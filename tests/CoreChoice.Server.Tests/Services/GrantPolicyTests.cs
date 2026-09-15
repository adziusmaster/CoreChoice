using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoreChoice.Server.Tests.Services;

public class GrantPolicyTests
{
    private static async Task<(IGrantPolicy Policy, ICoinStore Coins)> BuildAsync(CoinOptions? options = null)
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);
        var coins = new SqliteCoinStore(factory);
        var opts = Options.Create(options ?? new CoinOptions());
        return (new SqliteGrantPolicy(factory, coins, opts), coins);
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
        var (policy, _) = await BuildAsync();
        var device = Guid.NewGuid();
        await policy.EnsureSeededAsync(device, "origin-a");
        await policy.TryGrantProfileCompletionAsync(device, "origin-a");

        // Act — the retry-after-dropped-connection case.
        var outcome = await policy.TryGrantProfileCompletionAsync(device, "origin-a");

        // Assert
        outcome.Granted.Should().BeFalse();
        outcome.Balance.Should().Be(10, "a retry must neither cost the grant nor duplicate it");
        outcome.Reason.Should().Be("already-granted");
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
