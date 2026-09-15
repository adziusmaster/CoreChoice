using CoreChoice.Domain;
using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;

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
        personas.Select(p => p.Id).Should().Contain("devils-advocate");
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
