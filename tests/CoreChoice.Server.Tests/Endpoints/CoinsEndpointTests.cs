using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using FluentAssertions;
using NSubstitute;

namespace CoreChoice.Server.Tests.Endpoints;

public class CoinsEndpointTests
{
    private static HttpClient Build() =>
        new CoreChoiceAppFactory(Substitute.For<IGeminiClient>()).CreateClient();

    [Fact]
    public async Task Ensure_OnFirstContact_ShouldSeedFiveCoins()
    {
        // Arrange
        var client = Build();
        var device = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BalanceDto>();
        body!.Balance.Should().Be(5);
    }

    [Fact]
    public async Task Ensure_WhenCalledTwice_ShouldNotSeedTwice()
    {
        // Arrange
        var client = Build();
        var device = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });

        // Act
        var response = await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });

        // Assert
        (await response.Content.ReadFromJsonAsync<BalanceDto>())!.Balance.Should().Be(5);
    }

    [Fact]
    public async Task GetBalance_ForAnUnknownDevice_ShouldReturnZeroRatherThanNotFound()
    {
        // Arrange — an unknown device is a new device, not an error.
        var client = Build();

        // Act
        var response = await client.GetAsync($"/api/coins/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BalanceDto>())!.Balance.Should().Be(0);
    }

    [Fact]
    public async Task ProfileGrant_OnFirstClaim_ShouldAddFiveCoins()
    {
        // Arrange
        var client = Build();
        var device = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });

        // Act
        var response = await client.PostAsJsonAsync("/api/coins/profile-grant", new { deviceId = device });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GrantDto>();
        body!.Granted.Should().BeTrue();
        body.Balance.Should().Be(10, "five seeded plus five for finishing the test");
    }

    [Fact]
    public async Task ProfileGrant_WhenRetried_ShouldReturnTheSameBalanceAndNotGrantAgain()
    {
        // Arrange
        var client = Build();
        var device = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });
        await client.PostAsJsonAsync("/api/coins/profile-grant", new { deviceId = device });

        // Act — the dropped-connection retry.
        var response = await client.PostAsJsonAsync("/api/coins/profile-grant", new { deviceId = device });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GrantDto>();
        body!.Granted.Should().BeFalse();
        body.Balance.Should().Be(10);
    }

    [Fact]
    public async Task ProfileGrant_ShouldNeverRequireCoinsToClaim()
    {
        // Arrange — the free-result invariant, asserted at the boundary: a device with a zero
        // balance that finished the test still receives its grant. Nothing about showing a result
        // may ever consult the ledger.
        var client = Build();
        var device = Guid.NewGuid();   // deliberately not seeded: balance is zero

        // Act
        var response = await client.PostAsJsonAsync("/api/coins/profile-grant", new { deviceId = device });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GrantDto>();
        body!.Granted.Should().BeTrue();
        body.Balance.Should().Be(5);
    }

    [Fact]
    public async Task Ensure_WithAnEmptyDeviceId_ShouldReturnBadRequest()
    {
        // Arrange
        var client = Build();

        // Act
        var response = await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = Guid.Empty });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record BalanceDto(Guid DeviceId, int Balance);
    private sealed record GrantDto(Guid DeviceId, int Balance, bool Granted, string? Reason);
}
