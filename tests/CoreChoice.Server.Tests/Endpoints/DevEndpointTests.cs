using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace CoreChoice.Server.Tests.Endpoints;

/// <summary>
/// The dev grant endpoint hands out free coins behind a shared secret. It had no tests at all
/// before this file — the 404-when-unset path, the 404-on-wrong-secret path and the endpoint's own
/// validation were all correct but entirely unguarded.
/// </summary>
public class DevEndpointTests
{
    private const string Secret = "correct-horse-battery-staple";

    private static HttpClient BuildWithSecret(string? secret = Secret)
    {
        var factory = new CoreChoiceAppFactory(Substitute.For<IGeminiClient>());
        var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
            {
                if (secret is not null)
                    config.AddInMemoryCollection([new("Dev:Secret", secret)]);
            }));
        return configured.CreateClient();
    }

    [Fact]
    public async Task Grant_WhenTheSecretIsUnset_ShouldReturnNotFound()
    {
        // Arrange — no "Dev:Secret" configured at all.
        var client = BuildWithSecret(secret: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/dev/grant",
            new { deviceId = Guid.NewGuid(), amount = 5 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Grant_WithTheWrongSecret_ShouldReturnNotFound()
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Dev-Secret", "not-the-secret");

        // Act
        var response = await client.PostAsJsonAsync("/api/dev/grant",
            new { deviceId = Guid.NewGuid(), amount = 5 });

        // Assert — 404, not 401: an endpoint that says "wrong secret" has confirmed it exists.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Grant_WithTheCorrectSecret_ShouldGrantTheCoins()
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Dev-Secret", Secret);
        var device = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync("/api/dev/grant", new { deviceId = device, amount = 7 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BalanceDto>();
        body!.Balance.Should().Be(7);

        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}");
        balance!.Balance.Should().Be(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Grant_WithANonPositiveAmount_ShouldReturnBadRequest(int amount)
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Dev-Secret", Secret);

        // Act
        var response = await client.PostAsJsonAsync("/api/dev/grant",
            new { deviceId = Guid.NewGuid(), amount });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grant_WithAnEmptyDeviceId_ShouldReturnBadRequest()
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Dev-Secret", Secret);

        // Act
        var response = await client.PostAsJsonAsync("/api/dev/grant",
            new { deviceId = Guid.Empty, amount = 5 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_WhenTheSecretIsUnset_ShouldReturnNotFoundNotMethodNotAllowed()
    {
        // Arrange — the route must not exist at all when unconfigured, not merely reject the verb:
        // a 405 would confirm the route's existence to a prober.
        var client = BuildWithSecret(secret: null);

        // Act
        var response = await client.GetAsync("/api/dev/grant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_WithAMalformedBody_WhenTheSecretIsUnset_ShouldReturnNotFoundNotBadRequest()
    {
        // Arrange — a malformed body must not confirm the route's existence via a 400 either:
        // argument binding runs before the endpoint filter, so the route itself must be absent.
        var client = BuildWithSecret(secret: null);
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/dev/grant", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record BalanceDto(Guid DeviceId, int Balance);
}
