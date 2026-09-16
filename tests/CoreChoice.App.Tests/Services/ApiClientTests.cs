using System.Net;
using System.Text.Json;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoreChoice.App.Tests.Services;

public class ApiClientTests
{
    private static readonly Guid DeviceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly OceanProfile ScoredProfile = new(
        TraitScore.From(80), TraitScore.From(55), TraitScore.From(40), TraitScore.From(70), TraitScore.From(20));

    private const string DecisionResponseJson = """
        {
          "recommendation": "Take the job in Berlin",
          "confidence": 68,
          "reasoning": ["The upside compounds", "The downside is bounded"],
          "optionA": { "strengths": ["Growth"], "risks": ["Uprooting"] },
          "optionB": { "strengths": ["Stability"], "risks": ["Stagnation"] },
          "personalityNote": "Your high openness favours the unfamiliar option.",
          "personalized": true,
          "balance": 4
        }
        """;

    private static IDeviceIdentity FakeDeviceIdentity()
    {
        var identity = Substitute.For<IDeviceIdentity>();
        identity.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(DeviceId);
        return identity;
    }

    private static CoreChoiceApiClient Build(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            FakeDeviceIdentity(),
            Options.Create(new ApiOptions()));

    private static DecisionRequest RequestWith(OceanProfile profile) =>
        new(
            Dilemma.Create("Take the Berlin job", "Stay in Rotterdam", "Family is here"),
            profile,
            PersonaId.From("pragmatic-friend"),
            DecisionWeight.From(4));

    [Fact]
    public async Task AnalyseAsync_WithASuccessfulResponse_ShouldMapEveryField()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(DecisionResponseJson));
        var client = Build(handler);

        // Act
        var result = await client.AnalyseAsync(RequestWith(ScoredProfile));

        // Assert
        result.Analysis.Recommendation.Should().Be("Take the job in Berlin");
        result.Analysis.Confidence.Should().Be(68);
        result.Analysis.Reasoning.Should().Equal("The upside compounds", "The downside is bounded");
        result.Analysis.OptionA.Strengths.Should().Equal("Growth");
        result.Analysis.OptionA.Risks.Should().Equal("Uprooting");
        result.Analysis.OptionB.Strengths.Should().Equal("Stability");
        result.Analysis.OptionB.Risks.Should().Equal("Stagnation");
        result.Analysis.PersonalityNote.Should().Be("Your high openness favours the unfamiliar option.");
        result.Analysis.IsPersonalized.Should().BeTrue();
        result.Balance.Should().Be(4);
    }

    [Fact]
    public async Task AnalyseAsync_When402_ShouldThrowInsufficientCoins()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(
                """{"error":"Not enough coins.","required":1,"balance":0}""", HttpStatusCode.PaymentRequired));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync(RequestWith(ScoredProfile));

        // Assert
        var ex = await act.Should().ThrowAsync<InsufficientCoinsException>();
        ex.Which.Required.Should().Be(1);
        ex.Which.Available.Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_When503_ShouldThrowDecisionUnavailable()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(
                """{"error":"The advisor is temporarily unavailable. Please try again."}""",
                HttpStatusCode.ServiceUnavailable));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync(RequestWith(ScoredProfile));

        // Assert
        await act.Should().ThrowAsync<DecisionUnavailableException>();
    }

    [Fact]
    public async Task AnalyseAsync_When502_ShouldThrowMalformedAdvisorResponse()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(
                """{"error":"The advisor returned an unusable response."}""", HttpStatusCode.BadGateway));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync(RequestWith(ScoredProfile));

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Fact]
    public async Task AnalyseAsync_When429_ShouldThrowDecisionUnavailableWithARateLimitMessage()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("""{"error":"Too many requests."}""", HttpStatusCode.TooManyRequests));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync(RequestWith(ScoredProfile));

        // Assert
        var ex = await act.Should().ThrowAsync<DecisionUnavailableException>();
        ex.Which.Message.Should().ContainEquivalentOf("many requests");
    }

    [Fact]
    public async Task AnalyseAsync_WhenProfileIsNone_ShouldSendANullProfile()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(DecisionResponseJson));
        var client = Build(handler);

        // Act
        await client.AnalyseAsync(RequestWith(OceanProfile.None));

        // Assert
        using var doc = JsonDocument.Parse(handler.RequestBodies.Single());
        doc.RootElement.GetProperty("profile").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AnalyseAsync_WhenProfileIsScored_ShouldSendFiveIntegers()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(DecisionResponseJson));
        var client = Build(handler);

        // Act
        await client.AnalyseAsync(RequestWith(ScoredProfile));

        // Assert
        using var doc = JsonDocument.Parse(handler.RequestBodies.Single());
        var profile = doc.RootElement.GetProperty("profile");
        profile.ValueKind.Should().Be(JsonValueKind.Object);
        profile.GetProperty("openness").GetInt32().Should().Be(80);
        profile.GetProperty("conscientiousness").GetInt32().Should().Be(55);
        profile.GetProperty("extraversion").GetInt32().Should().Be(40);
        profile.GetProperty("agreeableness").GetInt32().Should().Be(70);
        profile.GetProperty("neuroticism").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task EnsureSeededAsync_ShouldPostTheDeviceIdAndReadTheBalanceBack()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json($$"""{"deviceId":"{{DeviceId}}","balance":5}"""));
        var client = Build(handler);

        // Act
        var result = await client.EnsureSeededAsync();

        // Assert
        result.Balance.Should().Be(5);
        using var doc = JsonDocument.Parse(handler.RequestBodies.Single());
        doc.RootElement.GetProperty("deviceId").GetGuid().Should().Be(DeviceId);
    }

    [Fact]
    public async Task ClaimProfileGrantAsync_WhenGranted_ShouldMapGrantedAndReason()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json($$"""{"deviceId":"{{DeviceId}}","balance":10,"granted":true,"reason":null}"""));
        var client = Build(handler);

        // Act
        var result = await client.ClaimProfileGrantAsync();

        // Assert
        result.Granted.Should().BeTrue();
        result.Balance.Should().Be(10);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task ClaimProfileGrantAsync_WhenAlreadyGranted_ShouldMapTheReason()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(
                $$"""{"deviceId":"{{DeviceId}}","balance":10,"granted":false,"reason":"already-granted"}"""));
        var client = Build(handler);

        // Act
        var result = await client.ClaimProfileGrantAsync();

        // Assert
        result.Granted.Should().BeFalse();
        result.Reason.Should().Be("already-granted");
    }

    [Fact]
    public async Task GetPersonasAsync_ShouldMapEachSummary()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(
                """[{"id":"pragmatic-friend","displayName":"The Pragmatic Friend","description":"Weighs tradeoffs plainly."}]"""));
        var client = Build(handler);

        // Act
        var personas = await client.GetPersonasAsync();

        // Assert
        personas.Should().ContainSingle();
        personas[0].Id.Should().Be("pragmatic-friend");
        personas[0].DisplayName.Should().Be("The Pragmatic Friend");
        personas[0].Description.Should().Be("Weighs tradeoffs plainly.");
    }
}
