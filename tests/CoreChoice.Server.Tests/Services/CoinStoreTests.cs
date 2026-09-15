using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CoreChoice.Server.Tests.Services;

public class CoinStoreTests
{
    private static async Task<(ICoinStore Store, IDbContextFactory<ServerDbContext> Factory)> BuildAsync(
        params IInterceptor[] interceptors)
    {
        var factory = InMemoryDb.Create(interceptors);
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

        // Act — deliberately a DIFFERENT initialCoins than the first call: an implementation that
        // wrongly echoed its own argument back instead of reading the persisted balance would
        // return 99 here, not 5. Only the persisted value makes this assertion pass.
        var balance = await store.EnsureDeviceAsync(device, initialCoins: 99);

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

    [Fact]
    public async Task GrantAsync_ForADeviceThatDoesNotExistYet_ShouldCreateTheRow()
    {
        // Arrange — a fresh device, never seeded via EnsureDeviceAsync. GrantAsync's doc comment
        // promises "creating the row if needed"; that branch had no coverage at all.
        var (store, _) = await BuildAsync();
        var device = Guid.NewGuid();

        // Act
        var balance = await store.GrantAsync(device, amount: 7);

        // Assert
        balance.Should().Be(7);
        (await store.GetBalanceAsync(device)).Should().Be(7);
    }

    [Fact]
    public async Task TrySpendAsync_ShouldDeductInOneGuardedStatement_NotReadThenWrite()
    {
        // Arrange — WHY this test exists: a read-then-write (load row, check balance in C#,
        // subtract, SaveChangesAsync) loses the double-tap race between two concurrent spenders
        // against a balance of one, which is exactly the scenario this store must prevent. But no
        // *behavioural* test running on a single connection can tell a read-then-write apart from
        // a single guarded UPDATE — both return the same booleans and leave the same final
        // balance when called back-to-back (confirmed: the whole suite passed against a
        // read-then-write rewrite during review). So instead of asserting on behaviour, this test
        // pins the SQL *shape* directly: exactly one statement, a guarded UPDATE, no SELECT. That
        // fails deterministically — every time, in both directions — against a read-then-write.
        var interceptor = new CommandRecordingInterceptor();
        var (store, _) = await BuildAsync(interceptor);
        var device = Guid.NewGuid();
        await store.EnsureDeviceAsync(device, initialCoins: 1);
        interceptor.Clear();

        // Act
        var spent = await store.TrySpendAsync(device, amount: 1);

        // Assert
        spent.Should().BeTrue();

        var commands = interceptor.Commands;
        commands.Should().HaveCount(1,
            "a guarded UPDATE is a single round trip to the database; a read-then-write needs at least two");

        var command = commands.Single();
        command.Should().Contain("UPDATE");
        command.Should().MatchRegex("\"Balance\"\\s*>=",
            "the WHERE clause must guard on Balance, not just touch the word 'Balance' anywhere");
        command.Should().NotMatchRegex("(?is)SELECT.*Coins",
            "a read-then-write would issue a SELECT against Coins before the UPDATE; none should appear here");
    }

    [Fact]
    public async Task TrySpendAsync_WithManyRealConnectionsRacingForTheLastCoin_ShouldLetExactlyOneWin()
    {
        // This test demonstrates correct behaviour under genuine multi-connection concurrency:
        // a file-backed database (not :memory:), WAL mode, a generous busy_timeout, and N
        // independent connections released together from a barrier. What it does NOT prove: per
        // measurement taken while building this suite, this style of test caught a naive
        // read-then-write reintroduction only intermittently (0/10 unsynchronised, 3/10-7/10 with
        // a barrier) — real hardware is often fast enough that the read/write window doesn't get
        // hit. TrySpendAsync_ShouldDeductInOneGuardedStatement_NotReadThenWrite above is the
        // reliable regression trap; this test's job is to prove the shipped implementation is
        // actually safe under real concurrency, not to catch every possible regression.
        const int concurrency = 20;
        var dbPath = Path.Combine(Path.GetTempPath(), $"corechoice-coin-{Guid.NewGuid():N}.db");

        try
        {
            var connectionString = $"Data Source={dbPath};Default Timeout=10";

            // WAL is a persistent, on-disk setting — set it once via its own connection before
            // any of the racing connections open. "Default Timeout=10" configures a 10s
            // busy_timeout on every connection opened from this connection string.
            await using (var setup = new SqliteConnection(connectionString))
            {
                await setup.OpenAsync();
                await using var pragma = setup.CreateCommand();
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                await pragma.ExecuteNonQueryAsync();
            }

            var factory = new FileConnectionFactory(connectionString);
            await SchemaInitializer.InitializeAsync(factory);
            var store = new SqliteCoinStore(factory);

            var device = Guid.NewGuid();
            await store.EnsureDeviceAsync(device, initialCoins: 1);

            // A barrier that releases every waiting task at once, with continuations forced onto
            // the thread pool so they don't just run one-by-one on whichever thread calls
            // SetResult.
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = Enumerable.Range(0, concurrency)
                .Select(async _ =>
                {
                    await barrier.Task;
                    return await store.TrySpendAsync(device, amount: 1);
                })
                .ToArray();

            barrier.SetResult();
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Count(r => r).Should().Be(1, "exactly one of the N racers must win a balance of one");
            (await store.GetBalanceAsync(device)).Should().Be(0);
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

    /// <summary>
    /// Unlike <see cref="InMemoryDb"/>'s single shared connection, this factory hands out a new
    /// <see cref="ServerDbContext"/> backed by its own connection to the same file each time —
    /// several genuinely independent connections against one on-disk database, which is what a
    /// real multi-request concurrency test needs.
    /// </summary>
    private sealed class FileConnectionFactory(string connectionString) : IDbContextFactory<ServerDbContext>
    {
        public ServerDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ServerDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new ServerDbContext(options);
        }
    }
}
