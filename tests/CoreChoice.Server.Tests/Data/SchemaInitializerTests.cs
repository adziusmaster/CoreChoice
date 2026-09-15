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
    public async Task InitializeAsync_WhenRunTwice_ShouldPreserveExistingData()
    {
        // Arrange — boot runs this every start against a live database holding real coin
        // balances. "Does not throw" is far too weak a guarantee: an initializer that dropped and
        // recreated its tables would satisfy it while wiping every balance on the volume. So the
        // test writes a row, re-runs initialization, and demands the row survive.
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);

        var deviceId = Guid.NewGuid();
        await using (var seed = await factory.CreateDbContextAsync())
        {
            seed.Coins.Add(new DeviceCoins
            {
                DeviceId = deviceId,
                Balance = 7,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await SchemaInitializer.InitializeAsync(factory);

        // Assert
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.Coins.SingleOrDefaultAsync(x => x.DeviceId == deviceId);
        row.Should().NotBeNull();
        row!.Balance.Should().Be(7, "re-initialising must never disturb persisted balances");
    }
}
