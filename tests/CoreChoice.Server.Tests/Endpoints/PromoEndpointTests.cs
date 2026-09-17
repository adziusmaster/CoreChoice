using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace CoreChoice.Server.Tests.Endpoints;

/// <summary>
/// End-to-end coverage of the public redeem route and the admin create/list/revoke routes, hosted
/// over the real application so routing, DI and the rate limiter are all exercised for real.
/// </summary>
public class PromoEndpointTests
{
    private const string Secret = "correct-horse-battery-staple";

    private static HttpClient BuildWithSecret(string? secret = Secret)
    {
        var factory = new CoreChoiceAppFactory(Substitute.For<IGeminiClient>());
        var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
            {
                if (secret is not null)
                    config.AddInMemoryCollection([new("Admin:Secret", secret)]);
            }));
        return configured.CreateClient();
    }

    private static async Task<PromoDto> CreatePromoAsync(HttpClient client, int coins, int? expiresInDays = null)
    {
        client.DefaultRequestHeaders.Remove("X-Admin-Secret");
        client.DefaultRequestHeaders.Add("X-Admin-Secret", Secret);
        var response = await client.PostAsJsonAsync("/api/admin/promo", new { coins, expiresInDays });
        client.DefaultRequestHeaders.Remove("X-Admin-Secret");
        return (await response.Content.ReadFromJsonAsync<PromoDto>())!;
    }

    [Fact]
    public async Task Redeem_WithAValidCode_ShouldGrantCoins()
    {
        // Arrange
        var client = BuildWithSecret();
        var promo = await CreatePromoAsync(client, coins: 8);
        var device = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync("/api/promo/redeem", new { deviceId = device, code = promo.Code });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RedeemDto>();
        body!.CoinsGranted.Should().Be(8);
        body.Balance.Should().Be(8);
    }

    [Fact]
    public async Task Redeem_WhenTheSameDeviceRedeemsTwice_ShouldReturnConflictTheSecondTimeAndGrantNothingMore()
    {
        // Arrange
        var client = BuildWithSecret();
        var promo = await CreatePromoAsync(client, coins: 3);
        var device = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/promo/redeem", new { deviceId = device, code = promo.Code });

        // Act
        var second = await client.PostAsJsonAsync("/api/promo/redeem", new { deviceId = device, code = promo.Code });

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}");
        balance!.Balance.Should().Be(3, "the second, refused redemption must not grant anything more");
    }

    [Fact]
    public async Task Redeem_WithAnUnknownCode_ShouldReturnNotFound()
    {
        // Arrange
        var client = BuildWithSecret();

        // Act
        var response = await client.PostAsJsonAsync("/api/promo/redeem",
            new { deviceId = Guid.NewGuid(), code = "ZZZZZ" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Redeem_WithAnEmptyDeviceId_ShouldReturnBadRequest()
    {
        // Arrange
        var client = BuildWithSecret();

        // Act
        var response = await client.PostAsJsonAsync("/api/promo/redeem",
            new { deviceId = Guid.Empty, code = "ABCDE" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminRoutes_WhenTheSecretIsUnset_ShouldReturnNotFound()
    {
        // Arrange — the route must not exist at all when unconfigured.
        var client = BuildWithSecret(secret: null);

        // Act
        var response = await client.GetAsync("/api/admin/promo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminRoutes_PostWithAMalformedBody_WhenTheSecretIsUnset_ShouldReturnNotFoundNotBadRequest()
    {
        // Arrange — a malformed body must not confirm the route's existence via a 400 either:
        // argument binding runs before the endpoint filter, so the route itself must be absent.
        var client = BuildWithSecret(secret: null);
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/admin/promo", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminRoutes_WithTheWrongSecret_ShouldReturnUnauthorized()
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Admin-Secret", "not-the-secret");

        // Act
        var response = await client.GetAsync("/api/admin/promo");

        // Assert — 401, not 404: once a secret is configured the routes' existence is no longer a
        // secret, so the response should say the credential was wrong.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_List_Revoke_WithTheCorrectSecret_ShouldManageTheCode()
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Admin-Secret", Secret);

        // Act
        var created = await client.PostAsJsonAsync("/api/admin/promo", new { coins = 4, code = "TEST1" });
        var list = await client.GetFromJsonAsync<List<PromoDto>>("/api/admin/promo");
        var revoke = await client.PostAsync("/api/admin/promo/TEST1/revoke", null);
        var afterList = await client.GetFromJsonAsync<List<PromoDto>>("/api/admin/promo");

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Should().ContainSingle(p => p.Code == "TEST1" && !p.Revoked);
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        afterList!.Single(p => p.Code == "TEST1").Revoked.Should().BeTrue();
    }

    [Fact]
    public async Task Revoke_ForAnUnknownCode_ShouldReturnNotFound()
    {
        // Arrange
        var client = BuildWithSecret();
        client.DefaultRequestHeaders.Add("X-Admin-Secret", Secret);

        // Act
        var response = await client.PostAsync("/api/admin/promo/NOPE1/revoke", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Redeem_WhenTheBudgetIsExhausted_ShouldReturnTooManyRequests()
    {
        // Arrange — 10 permitted per minute, so the 11th must be refused.
        var client = BuildWithSecret(secret: null);

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/api/promo/redeem", new { deviceId = Guid.NewGuid(), code = "AAAAA" });

        // Assert
        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private sealed record PromoDto(
        string Code, int Coins, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool Revoked, int RedemptionCount);

    private sealed record RedeemDto(int CoinsGranted, int Balance);

    private sealed record BalanceDto(Guid DeviceId, int Balance);
}
