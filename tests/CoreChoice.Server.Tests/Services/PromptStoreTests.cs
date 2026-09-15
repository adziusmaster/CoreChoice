using CoreChoice.Ai;
using CoreChoice.Domain;
using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Tests.Services;

public class PromptStoreTests
{
    private static async Task<IPromptStore> BuildSeededAsync()
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        await ContentSeed.SeedAsync(factory);
        return new SqlitePromptStore(factory);
    }

    private static async Task<(IPromptStore Store, IDbContextFactory<ServerDbContext> Factory)> BuildSeededWithFactoryAsync()
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        await ContentSeed.SeedAsync(factory);
        return (new SqlitePromptStore(factory), factory);
    }

    [Fact]
    public async Task GetActivePersonasAsync_AfterSeeding_ShouldReturnSixOrdered()
    {
        // Arrange
        var store = await BuildSeededAsync();

        // Act
        var personas = await store.GetActivePersonasAsync();

        // Assert
        personas.Should().HaveCount(6);
        personas.Select(p => p.SortOrder).Should().BeInAscendingOrder();
        personas.Select(p => p.Id).Should().Equal(
            "devils-advocate", "warm-support", "pure-logic",
            "the-pragmatist", "the-long-view", "gut-check");
    }

    [Fact]
    public async Task GetActivePromptAsync_ForASeededPersona_ShouldReturnVersionOne()
    {
        // Arrange
        var store = await BuildSeededAsync();

        // Act
        var prompt = await store.GetActivePromptAsync(PersonaId.From("pure-logic"));

        // Assert
        prompt.Should().NotBeNull();
        prompt!.Version.Should().Be(1);
        prompt.Template.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetActivePromptAsync_ForAnUnknownPersona_ShouldReturnNull()
    {
        // Arrange
        var store = await BuildSeededAsync();

        // Act
        var prompt = await store.GetActivePromptAsync(PersonaId.From("no-such-persona"));

        // Assert
        prompt.Should().BeNull();
    }

    [Fact]
    public async Task GetActivePromptAsync_ForADeactivatedPersona_ShouldReturnNull()
    {
        // Arrange
        var (store, factory) = await BuildSeededWithFactoryAsync();

        // Deactivate pure-logic persona by writing directly through the factory. The Join clause
        // that filters on p.IsActive is otherwise dead code — a reviewer could delete it and all
        // tests would still pass. A persona that is switched off must stop answering rather than
        // keep serving its old prompt.
        await using (var db = await factory.CreateDbContextAsync())
        {
            var persona = await db.Personas.FirstAsync(p => p.Id == "pure-logic");
            persona.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Act
        var prompt = await store.GetActivePromptAsync(PersonaId.From("pure-logic"));

        // Assert
        prompt.Should().BeNull();
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_ShouldNotDuplicateRows()
    {
        // Arrange
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        await ContentSeed.SeedAsync(factory);

        // Act
        await ContentSeed.SeedAsync(factory);

        // Assert
        var store = new SqlitePromptStore(factory);
        (await store.GetActivePersonasAsync()).Should().HaveCount(6);

        // Guard against duplicating templates: a guard that stops duplicating personas but not
        // templates would currently pass, so both must be checked.
        await using var db = await factory.CreateDbContextAsync();
        (await db.PromptTemplates.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task EverySeededPersona_ShouldHaveAnActivePrompt()
    {
        // Arrange
        var store = await BuildSeededAsync();
        var personas = await store.GetActivePersonasAsync();

        // Act
        var prompts = new List<ActivePrompt?>();
        foreach (var persona in personas)
            prompts.Add(await store.GetActivePromptAsync(PersonaId.From(persona.Id)));

        // Assert — a persona the picker offers but the server cannot answer for is a 500 waiting
        // to happen, so the pairing is asserted rather than assumed.
        prompts.Should().NotContainNulls();
    }

    [Fact]
    public async Task EverySeededTemplate_RunThroughTheRealAssembler_ShouldSubstituteBothPlaceholders()
    {
        // This is a round-trip test across the Server<->Core seam: the templates are DATA seeded by
        // ContentSeed.cs, carrying the literal markers {{PROFILE}} and {{WEIGHT}}, while
        // PromptAssembler (Core project) substitutes them in CODE. Nothing else ties the two halves
        // together — a rename or deletion on either side can leave the other silently broken.
        //
        // Assertion 1 (no leftover "{{") only catches a rename/typo, where the marker text survives
        // but no longer matches the assembler's constant. It CANNOT catch outright deletion of a
        // marker from the template: if {{PROFILE}} is deleted from the seed text entirely,
        // string.Replace finds no match and there is no stray brace left behind to detect, because
        // nothing was there to strand. This was verified by reproduction (deleting the {{PROFILE}}
        // line from ContentSeed.cs and confirming assertion 1 alone stays green).
        //
        // Assertion 2 (profile-derived content actually appears, tied to its trait name and value)
        // is the one that catches deletion, because it fails not on leftover syntax but on the
        // personalization content simply never having made it into the string. Do not "simplify"
        // this test down to assertion 1 alone — that removes the only check that catches the
        // deletion case this test exists for.

        // Arrange
        var store = await BuildSeededAsync();
        var personas = await store.GetActivePersonasAsync();

        var profile = new OceanProfile(
            TraitScore.From(85), TraitScore.From(40), TraitScore.From(20),
            TraitScore.From(75), TraitScore.From(55));
        var weight = DecisionWeight.From(4);
        var dilemma = Dilemma.Create("Take the new job", "Stay in my current role");

        personas.Should().NotBeEmpty();

        foreach (var persona in personas)
        {
            var activePrompt = await store.GetActivePromptAsync(PersonaId.From(persona.Id));
            activePrompt.Should().NotBeNull($"persona '{persona.Id}' is active and must have a prompt");

            // Act
            var assembled = PromptAssembler.Assemble(activePrompt!.Template, profile, weight, dilemma);

            // Assert 1 — catches a rename/typo of a marker on either side of the seam.
            assembled.SystemPrompt.Should().NotContain("{{",
                $"persona '{persona.Id}' should have no unresolved placeholder syntax left");

            // Assert 2 — catches outright deletion of a marker, which leaves no braces behind.
            // Tie the value to its trait label so a mix-up elsewhere couldn't coincidentally pass.
            assembled.SystemPrompt.Should().Contain("openness: 85 (high)",
                $"persona '{persona.Id}' must actually carry the substituted profile content, " +
                "not merely lack leftover braces");
            assembled.SystemPrompt.Should().Contain(weight.Description,
                $"persona '{persona.Id}' must actually carry the substituted weight content");
        }
    }
}
