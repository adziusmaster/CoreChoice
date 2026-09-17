using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Tests.Services;

public class PromoStoreTests
{
    private static async Task<(IPromoStore Promos, ICoinStore Coins, IDbContextFactory<ServerDbContext> Factory)>
        BuildAsync()
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        var coins = new SqliteCoinStore(factory);
        return (new SqlitePromoStore(factory, coins), coins, factory);
    }

    [Fact]
    public async Task RedeemAsync_WithAValidCode_ShouldGrantExactlyTheConfiguredCoins()
    {
        // Arrange
        var (promos, coins, _) = await BuildAsync();
        var promo = await promos.CreateAsync(code: null, coins: 7, expiresInDays: null);
        var device = Guid.NewGuid();

        // Act
        var result = await promos.RedeemAsync(device, promo.Code);

        // Assert
        result.Outcome.Should().Be(RedeemOutcome.Success);
        result.CoinsGranted.Should().Be(7);
        result.Balance.Should().Be(7);
        (await coins.GetBalanceAsync(device)).Should().Be(7);
    }

    [Fact]
    public async Task RedeemAsync_WhenTheSameDeviceRedeemsTwice_ShouldRefuseAndGrantNothingTheSecondTime()
    {
        // Arrange
        var (promos, coins, _) = await BuildAsync();
        var promo = await promos.CreateAsync(null, 5, null);
        var device = Guid.NewGuid();
        await promos.RedeemAsync(device, promo.Code);

        // Act
        var second = await promos.RedeemAsync(device, promo.Code);

        // Assert
        second.Outcome.Should().Be(RedeemOutcome.AlreadyRedeemed);
        second.CoinsGranted.Should().Be(0);
        (await coins.GetBalanceAsync(device)).Should().Be(5, "the second redemption must not grant additional coins");
    }

    [Fact]
    public async Task RedeemAsync_WithTwoDifferentDevices_ShouldLetBothRedeemTheSameCode()
    {
        // Arrange
        var (promos, coins, _) = await BuildAsync();
        var promo = await promos.CreateAsync(null, 4, null);
        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();

        // Act
        var resultA = await promos.RedeemAsync(deviceA, promo.Code);
        var resultB = await promos.RedeemAsync(deviceB, promo.Code);

        // Assert
        resultA.Outcome.Should().Be(RedeemOutcome.Success);
        resultB.Outcome.Should().Be(RedeemOutcome.Success);
        (await coins.GetBalanceAsync(deviceA)).Should().Be(4);
        (await coins.GetBalanceAsync(deviceB)).Should().Be(4);
    }

    [Fact]
    public async Task RedeemAsync_WithAnExpiredCode_ShouldRefuseAndGrantNothing()
    {
        // Arrange
        var (promos, coins, factory) = await BuildAsync();
        var promo = await promos.CreateAsync(null, 5, expiresInDays: 1);
        // Force the expiry into the past directly; CreateAsync only accepts days-from-now.
        await using (var db = await factory.CreateDbContextAsync())
        {
            var row = await db.PromoCodes.FirstAsync(x => x.Code == promo.Code);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        var device = Guid.NewGuid();

        // Act
        var result = await promos.RedeemAsync(device, promo.Code);

        // Assert
        result.Outcome.Should().Be(RedeemOutcome.Expired);
        result.CoinsGranted.Should().Be(0);
        (await coins.GetBalanceAsync(device)).Should().Be(0);
    }

    [Fact]
    public async Task RedeemAsync_WithARevokedCode_ShouldRefuseAndGrantNothing()
    {
        // Arrange
        var (promos, coins, _) = await BuildAsync();
        var promo = await promos.CreateAsync(null, 5, null);
        await promos.RevokeAsync(promo.Code);
        var device = Guid.NewGuid();

        // Act
        var result = await promos.RedeemAsync(device, promo.Code);

        // Assert
        result.Outcome.Should().Be(RedeemOutcome.Revoked);
        (await coins.GetBalanceAsync(device)).Should().Be(0);
    }

    [Fact]
    public async Task RedeemAsync_WithAnUnknownCode_ShouldRefuseAndGrantNothing()
    {
        // Arrange
        var (promos, coins, _) = await BuildAsync();
        var device = Guid.NewGuid();

        // Act
        var result = await promos.RedeemAsync(device, "ZZZZZ");

        // Assert
        result.Outcome.Should().Be(RedeemOutcome.NotFound);
        (await coins.GetBalanceAsync(device)).Should().Be(0);
    }

    [Fact]
    public async Task RevokeAsync_ForAnUnknownCode_ShouldReturnFalse()
    {
        // Arrange
        var (promos, _, _) = await BuildAsync();

        // Act
        var revoked = await promos.RevokeAsync("NOPE1");

        // Assert
        revoked.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_ShouldReportRedemptionCountsPerCode()
    {
        // Arrange
        var (promos, _, _) = await BuildAsync();
        var promo = await promos.CreateAsync(null, 3, null);
        await promos.RedeemAsync(Guid.NewGuid(), promo.Code);
        await promos.RedeemAsync(Guid.NewGuid(), promo.Code);

        // Act
        var summaries = await promos.ListAsync();

        // Assert
        summaries.Should().ContainSingle(s => s.Code == promo.Code && s.RedemptionCount == 2);
    }

    [Fact]
    public async Task RedeemAsync_WhenTheCallerDisconnectsAfterTheMarkerRowCommits_ShouldStillGrantTheCoins()
    {
        // Same shape as GrantPolicyTests' C-1 regression: the PromoRedemption marker row is written
        // and committed with the caller's token, then the coin grant runs through a separate
        // DbContext/connection with the SAME token. If that token is cancelled the instant the
        // marker row lands — the realistic shape of a dropped connection — a pre-fix RedeemAsync
        // loses the grant entirely: the device is left recorded as having redeemed the code while
        // holding none of the coins it promised, and every retry short-circuits as AlreadyRedeemed
        // forever. The interceptor cancels `cts` right after the marker row's SaveChangesAsync
        // completes, so the grant call is made with an already-cancelled token — exactly the window
        // the CancellationToken.None fix in SqlitePromoStore must survive.
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        try
        {
            var plainFactory = new OptionsFactory(
                new DbContextOptionsBuilder<ServerDbContext>().UseSqlite(connection).Options);
            await SchemaInitializer.InitializeAsync(plainFactory);
            var coins = new SqliteCoinStore(plainFactory);
            var promoSetup = new SqlitePromoStore(plainFactory, coins);
            var promo = await promoSetup.CreateAsync(null, 6, null);

            using var cts = new CancellationTokenSource();
            var interceptor = new CancelAfterFirstWriteInterceptor(cts);
            var interceptingFactory = new OptionsFactory(
                new DbContextOptionsBuilder<ServerDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options);
            var promoUnderTest = new SqlitePromoStore(interceptingFactory, coins);
            var device = Guid.NewGuid();

            // Act
            var result = await promoUnderTest.RedeemAsync(device, promo.Code, cts.Token);

            // Assert
            result.Outcome.Should().Be(RedeemOutcome.Success);
            result.Balance.Should().Be(6,
                "the redemption marker row was already committed, so the grant it promises must land even though the caller's token was cancelled the instant it committed");
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task RedeemAsync_WhenTwoConcurrentRedeemsRaceForTheSameDeviceAndCode_ShouldGrantExactlyOnce()
    {
        // A file-backed database (not :memory:, which serialises on a single connection and would
        // never let two calls both pass the pre-check before either writes), WAL mode, a generous
        // busy timeout, and two independent connections released together from a barrier — same
        // harness shape as GrantPolicyTests.TryGrantProfileCompletionAsync_WhenTwoClaimsRace....
        // This is the only way to actually drive execution through the `catch (DbUpdateException)`
        // branch in RedeemAsync, which no sequential test can reach — and it is the direct proof of
        // the once-per-device rule: two concurrent redeems by the same device must grant once.
        const int concurrency = 2;
        var dbPath = Path.Combine(Path.GetTempPath(), $"corechoice-promo-{Guid.NewGuid():N}.db");

        try
        {
            var connectionString = $"Data Source={dbPath};Default Timeout=10";

            await using (var setup = new SqliteConnection(connectionString))
            {
                await setup.OpenAsync();
                await using var pragma = setup.CreateCommand();
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                await pragma.ExecuteNonQueryAsync();
            }

            IDbContextFactory<ServerDbContext> factory = new FileConnectionFactory(connectionString);
            await SchemaInitializer.InitializeAsync(factory);
            var coins = new SqliteCoinStore(factory);
            var promos = new SqlitePromoStore(factory, coins);
            var promo = await promos.CreateAsync(null, 5, null);
            var device = Guid.NewGuid();

            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = Enumerable.Range(0, concurrency)
                .Select(async _ =>
                {
                    await barrier.Task;
                    return await promos.RedeemAsync(device, promo.Code);
                })
                .ToArray();

            barrier.SetResult();
            var outcomes = await Task.WhenAll(tasks);

            // Assert
            outcomes.Count(o => o.Outcome == RedeemOutcome.Success).Should().Be(1,
                "exactly one of the two racing redeems by the same device must win");
            (await coins.GetBalanceAsync(device)).Should().Be(5,
                "a race must produce exactly one grant's worth of coins, not two");

            await using var db = await factory.CreateDbContextAsync();
            (await db.PromoRedemptions.CountAsync(x => x.DeviceId == device)).Should().Be(1,
                "a race must leave exactly one redemption row, never zero or two");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
            TryDelete(dbPath + "-wal");
            TryDelete(dbPath + "-shm");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp file is not worth failing the test over.
        }
    }

    private sealed class OptionsFactory(DbContextOptions<ServerDbContext> options) : IDbContextFactory<ServerDbContext>
    {
        public ServerDbContext CreateDbContext() => new(options);
    }

    private sealed class FileConnectionFactory(string connectionString) : IDbContextFactory<ServerDbContext>
    {
        public ServerDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ServerDbContext>().UseSqlite(connectionString).Options;
            return new ServerDbContext(options);
        }
    }
}
