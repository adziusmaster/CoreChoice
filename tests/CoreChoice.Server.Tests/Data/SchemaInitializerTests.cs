using CoreChoice.Server.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Tests.Data;

public class SchemaInitializerTests
{
    [Fact]
    public async Task InitializeAsync_OnAFreshDatabase_ShouldCreateEverySet()
    {
        // Arrange
        var factory = InMemoryDb.Create();

        // Act
        await SchemaInitializer.InitializeAsync(factory);

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        (await db.Coins.CountAsync()).Should().Be(0);
        (await db.DeviceSeeds.CountAsync()).Should().Be(0);
        (await db.ProfileGrants.CountAsync()).Should().Be(0);
        (await db.UsageLogs.CountAsync()).Should().Be(0);

        // Personas and PromptTemplates are asserted as QUERYABLE, not as empty. Task 9 makes this
        // same initializer seed six personas at boot, which is a requirement — so asserting these
        // tables are empty would assert the opposite of what the system must do, and would break
        // the moment Task 9 lands.
        var queryPersonas = async () => await db.Personas.CountAsync();
        var queryTemplates = async () => await db.PromptTemplates.CountAsync();
        await queryPersonas.Should().NotThrowAsync();
        await queryTemplates.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_WhenRunTwice_ShouldNotThrow()
    {
        // Arrange
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);

        // Act
        var act = async () => await SchemaInitializer.InitializeAsync(factory);

        // Assert — boot runs this every start; it must be idempotent.
        await act.Should().NotThrowAsync();
    }
}
