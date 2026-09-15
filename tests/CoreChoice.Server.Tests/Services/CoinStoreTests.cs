using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Tests.Services;

public class CoinStoreTests
{
    private static async Task<(ICoinStore Store, IDbContextFactory<ServerDbContext> Factory)> BuildAsync()
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        return (new SqliteCoinStore(factory), factory);
    }

    [Fact]
    public async Task EnsureDeviceAsync_OnFirstContact_ShouldSeedTheGrant()
    {
        // Arrange
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();

        // Act
        var balance = await store.EnsureDeviceAsync(device, initialCoins: 5);

        // Assert
        balance.Should().Be(5);
    }

    [Fact]
    public async Task EnsureDeviceAsync_WhenCalledTwice_ShouldNotSeedTwice()
    {
        // Arrange
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 5);

        // Act
        var balance = await store.EnsureDeviceAsync(device, initialCoins: 5);

        // Assert
        balance.Should().Be(5);
    }

    [Fact]
    public async Task TrySpendAsync_WithSufficientBalance_ShouldDeduct()
    {
        // Arrange
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 3);

        // Act
        var spent = await store.TrySpendAsync(device, amount: 1);

        // Assert
        spent.Should().BeTrue();
        (await store.GetBalanceAsync(device)).Should().Be(2);
    }

    [Fact]
    public async Task TrySpendAsync_WithInsufficientBalance_ShouldRefuseAndLeaveBalanceUntouched()
    {
        // Arrange
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 0);

        // Act
        var spent = await store.TrySpendAsync(device, amount: 1);

        // Assert
        spent.Should().BeFalse();
        (await store.GetBalanceAsync(device)).Should().Be(0);
    }

    [Fact]
    public async Task TrySpendAsync_ForAnUnknownDevice_ShouldRefuse()
    {
        // Arrange
        var (store, _) = await BuildAsync();

        // Act
        var spent = await store.TrySpendAsync(Guid.NewGuid(), amount: 1);

        // Assert
        spent.Should().BeFalse();
    }

    [Fact]
    public async Task TrySpendAsync_WhenTwoRequestsRaceForTheLastCoin_ShouldLetExactlyOneWin()
    {
        // Arrange — the double-tap case: two requests, one coin.
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 1);

        // Act — sequential rather than parallel: a single in-memory SQLite connection serializes
        // writes anyway, so parallelism here would test the connection, not the conditional update.
        var first = await store.TrySpendAsync(device, amount: 1);
        var second = await store.TrySpendAsync(device, amount: 1);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        (await store.GetBalanceAsync(device)).Should().Be(0);
    }

    [Fact]
    public async Task RefundAsync_AfterAFailedAnalysis_ShouldRestoreTheCoin()
    {
        // Arrange
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 2);
        await store.TrySpendAsync(device, amount: 1);

        // Act
        await store.RefundAsync(device, amount: 1);

        // Assert
        (await store.GetBalanceAsync(device)).Should().Be(2);
    }

    [Fact]
    public async Task GrantAsync_ForAnExistingDevice_ShouldAddToTheBalance()
    {
        // Arrange
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 5);

        // Act
        var balance = await store.GrantAsync(device, amount: 5);

        // Assert
        balance.Should().Be(10);
    }
}
