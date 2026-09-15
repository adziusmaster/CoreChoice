using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoreChoice.Server.Tests.Services;

public class GrantPolicyTests
{
    private static async Task<(IGrantPolicy Policy, ICoinStore Coins, IDbContextFactory<ServerDbContext> Factory)>
        BuildWithFactoryAsync(CoinOptions? options = null)
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        var coins = new SqliteCoinStore(factory);
        var opts = Options.Create(options ?? new CoinOptions());
        return (new SqliteGrantPolicy(factory, coins, opts), coins, factory);
    }

    private static async Task<(IGrantPolicy Policy, ICoinStore Coins)> BuildAsync(CoinOptions? options = null)
    {
        var (policy, coins, _) = await BuildWithFactoryAsync(options);
        return (policy, coins);
    }

    [Fact]
    public async Task EnsureSeededAsync_OnFirstContact_ShouldGrantTheConfiguredAmount()
    {
        // Arrange
        var (policy, _) = await BuildAsync();
        var device = Guid.NewGuid();

        // Act
        var balance = await policy.EnsureSeededAsync(device, ipHash: "origin-a");

        // Assert
        balance.Should().Be(5);
    }

    [Fact]
    public async Task EnsureSeededAsync_WhenTheOriginCapIsReached_ShouldSeedZero()
    {
        // Arrange — cap of 2 grants per origin.
        var (policy, _) = await BuildAsync(new CoinOptions { MaxGrantsPerOrigin = 2 });
        await policy.EnsureSeededAsync(Guid.NewGuid(), "shared-origin");
        await policy.EnsureSeededAsync(Guid.NewGuid(), "shared-origin");

        // Act
        var balance = await policy.EnsureSeededAsync(Guid.NewGuid(), "shared-origin");

        // Assert
        balance.Should().Be(0);
    }

    [Fact]
    public async Task EnsureSeededAsync_ForADifferentOrigin_ShouldNotBeAffectedByAnotherOriginsCap()
    {
        // Arrange
        var (policy, _) = await BuildAsync(new CoinOptions { MaxGrantsPerOrigin = 1 });
        await policy.EnsureSeededAsync(Guid.NewGuid(), "origin-a");

        // Act
        var balance = await policy.EnsureSeededAsync(Guid.NewGuid(), "origin-b");

        // Assert
        balance.Should().Be(5);
    }

    [Fact]
    public async Task TryGrantProfileCompletionAsync_OnFirstClaim_ShouldAddTheGrant()
    {
        // Arrange
        var (policy, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await policy.EnsureSeededAsync(device, "origin-a");

        // Act
        var outcome = await policy.TryGrantProfileCompletionAsync(device, "origin-a");

        // Assert
        outcome.Granted.Should().BeTrue();
        outcome.Balance.Should().Be(10);
    }

    [Fact]
    public async Task TryGrantProfileCompletionAsync_WhenClaimedTwice_ShouldBeIdempotent()
    {
        // Arrange
        var (policy, _, factory) = await BuildWithFactoryAsync();
        var device = Guid.NewGuid();
        await policy.EnsureSeededAsync(device, "origin-a");
        await policy.TryGrantProfileCompletionAsync(device, "origin-a");

        // Act — the retry-after-dropped-connection case.
        var outcome = await policy.TryGrantProfileCompletionAsync(device, "origin-a");

        // Assert
        outcome.Granted.Should().BeFalse();
        outcome.Balance.Should().Be(10, "a retry must neither cost the grant nor duplicate it");
        outcome.Reason.Should().Be("already-granted");

        // The row count is the assertion that actually constrains the pre-check. If the pre-check
        // is removed, the second call falls through to the insert, and the untouched PK+catch
        // swallows the UNIQUE-constraint violation into the exact same GrantOutcome asserted above —
        // so without this, the test would keep passing with the pre-check deleted. Querying the
        // database directly (not through the policy) is the only way to still tell the two paths
        // apart.
        await using var db = await factory.CreateDbContextAsync();
        var grantCount = await db.ProfileGrants.CountAsync(x => x.DeviceId == device);
        grantCount.Should().Be(1, "exactly one ProfileGrant row must ever exist for this device");
    }

    [Fact]
    public async Task TryGrantProfileCompletionAsync_WhenTwoClaimsRaceForTheSameDevice_ShouldGrantExactlyOnce()
    {
        // A file-backed database (not :memory:, which serialises on a single connection and would
        // never let two calls both pass the pre-check before either writes), WAL mode, a generous
        // busy timeout, and two independent connections released together from a barrier — the same
        // harness shape proven in CoinStoreTests.
        // TrySpendAsync_WithManyRealConnectionsRacingForTheLastCoin_ShouldLetExactlyOneWin. This is
        // the only way to actually drive execution through the `catch (DbUpdateException)` branch in
        // TryGrantProfileCompletionAsync, which no sequential test can reach.
        const int concurrency = 2;
        var dbPath = Path.Combine(Path.GetTempPath(), $"corechoice-grant-{Guid.NewGuid():N}.db");

        try
        {
            var connectionString = $"Data Source={dbPath};Default Timeout=10";

            // WAL is a persistent, on-disk setting — set it once via its own connection before any
            // of the racing connections open. "Default Timeout=10" configures a 10s busy_timeout on
            // every connection opened from this connection string.
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
            var policy = new SqliteGrantPolicy(factory, coins, Options.Create(new CoinOptions()));
            var device = Guid.NewGuid();

            // A barrier that releases every waiting task at once, with continuations forced onto the
            // thread pool so they don't just run one-by-one on whichever thread calls SetResult.
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = Enumerable.Range(0, concurrency)
                .Select(async _ =>
                {
                    await barrier.Task;
                    return await policy.TryGrantProfileCompletionAsync(device, ipHash: null);
                })
                .ToArray();

            barrier.SetResult();
            var outcomes = await Task.WhenAll(tasks);

            // Assert
            outcomes.Count(o => o.Granted).Should().Be(1, "exactly one of the two racers must win the grant");

            await using var db = await factory.CreateDbContextAsync();
            (await db.ProfileGrants.CountAsync(x => x.DeviceId == device)).Should().Be(1,
                "a race must leave exactly one ProfileGrant row, never zero or two");
            (await coins.GetBalanceAsync(device)).Should().Be(5,
                "a race must produce exactly one grant's worth of coins, not two");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(dbPath);
            TryDelete(dbPath + "-wal");
            TryDelete(dbPath + "-shm");
        }
    }

    [Fact]
    public async Task EnsureSeededAsync_WithANullOrigin_ShouldNeverBeCapped()
    {
        // Arrange — cap of 1 grant per origin, already exhausted for a real origin. The null-origin
        // rule lives in IsOriginCappedAsync but was previously only exercised at the
        // ClientIpHasher.Hash(null) == null level, never at the policy layer where the rule
        // actually applies.
        var (policy, _) = await BuildAsync(new CoinOptions { MaxGrantsPerOrigin = 1 });
        await policy.EnsureSeededAsync(Guid.NewGuid(), "origin-a");

        // Act — several devices with no known origin, well past the cap that binds "origin-a".
        var balances = new List<int>();
        for (var i = 0; i < 3; i++)
            balances.Add(await policy.EnsureSeededAsync(Guid.NewGuid(), ipHash: null));

        // Assert
        balances.Should().AllSatisfy(b => b.Should().Be(5));
    }

    [Fact]
    public async Task TryGrantProfileCompletionAsync_WithANullOrigin_ShouldNeverBeCapped()
    {
        // Arrange — cap of 1 grant per origin, already exhausted for a real origin.
        var (policy, _) = await BuildAsync(new CoinOptions { MaxGrantsPerOrigin = 1 });
        await policy.TryGrantProfileCompletionAsync(Guid.NewGuid(), "origin-a");

        // Act — several devices with no known origin, well past the cap.
        var outcomes = new List<GrantOutcome>();
        for (var i = 0; i < 3; i++)
            outcomes.Add(await policy.TryGrantProfileCompletionAsync(Guid.NewGuid(), ipHash: null));

        // Assert
        outcomes.Should().AllSatisfy(o => o.Granted.Should().BeTrue());
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

    [Fact]
    public async Task TryGrantProfileCompletionAsync_WhenTheOriginCapIsReached_ShouldRefuse()
    {
        // Arrange
        var (policy, _) = await BuildAsync(new CoinOptions { MaxGrantsPerOrigin = 1 });
        await policy.TryGrantProfileCompletionAsync(Guid.NewGuid(), "farm");

        // Act
        var outcome = await policy.TryGrantProfileCompletionAsync(Guid.NewGuid(), "farm");

        // Assert
        outcome.Granted.Should().BeFalse();
        outcome.Reason.Should().Be("origin-cap");
    }

    [Fact]
    public void Hash_ForTheSameAddress_ShouldBeStableAndNotContainTheAddress()
    {
        // Arrange
        var hasher = new ClientIpHasher("a-fixed-salt");
        var address = System.Net.IPAddress.Parse("203.0.113.7");

        // Act
        var first = hasher.Hash(address);
        var second = hasher.Hash(address);

        // Assert
        first.Should().Be(second);
        first.Should().NotContain("203.0.113.7");
    }

    [Fact]
    public void Hash_WithADifferentSalt_ShouldDiffer()
    {
        // Arrange
        var address = System.Net.IPAddress.Parse("203.0.113.7");

        // Act
        var a = new ClientIpHasher("salt-one").Hash(address);
        var b = new ClientIpHasher("salt-two").Hash(address);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void HashDevice_ShouldBeStableAndNotContainTheDeviceId()
    {
        // Arrange
        var hasher = new ClientIpHasher("a-fixed-salt");
        var device = Guid.NewGuid();

        // Act
        var hash = hasher.HashDevice(device);

        // Assert
        hash.Should().Be(hasher.HashDevice(device));
        hash.Should().NotContain(device.ToString("N"));
    }

    [Fact]
    public void HashDevice_ShouldNotCollideWithAnAddressHash()
    {
        // Arrange — the two namespaces are separated by a prefix so a device id and an address can
        // never produce the same token and silently share a cap bucket.
        var hasher = new ClientIpHasher("a-fixed-salt");

        // Act
        var deviceHash = hasher.HashDevice(Guid.Empty);
        var ipHash = hasher.Hash(System.Net.IPAddress.Loopback);

        // Assert
        deviceHash.Should().NotBe(ipHash);
    }

    [Fact]
    public void Hash_ForAnUnknownAddress_ShouldReturnNull()
    {
        // Arrange
        var hasher = new ClientIpHasher("a-fixed-salt");

        // Act
        var hash = hasher.Hash(null);

        // Assert
        hash.Should().BeNull();
    }
}
