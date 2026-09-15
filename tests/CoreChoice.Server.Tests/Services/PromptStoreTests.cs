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
}
