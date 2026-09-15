using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using CoreChoice.Application;
using CoreChoice.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.Server.Tests.Endpoints;

public class DecisionEndpointTests
{
    private static DecisionResult Ok() => new(
        new DecisionAnalysis("Option A", 70, ["because"],
            new OptionAssessment("A", ["up"], ["down"]),
            new OptionAssessment("B", ["up"], ["down"]),
            "note", true),
        new TokenUsage(100, 50, 150));

    private static object Body(Guid device, string persona = "pure-logic", int weight = 3, object? profile = null) =>
        new
        {
            deviceId = device,
            optionA = "Take the job",
            optionB = "Stay put",
            context = (string?)null,
            persona,
            weight,
            profile,
        };

    private static object Profile() =>
        new { openness = 80, conscientiousness = 50, extraversion = 30, agreeableness = 60, neuroticism = 40 };

    private static async Task<(HttpClient Client, IGeminiClient Gemini)> BuildAsync(IGeminiClient? gemini = null)
    {
        gemini ??= Substitute.For<IGeminiClient>();
        var factory = new CoreChoiceAppFactory(gemini);
        var client = factory.CreateClient();

        // Seed the device's coins through the real endpoint.
        return (client, gemini);
    }

    private static async Task SeedAsync(HttpClient client, Guid device)
    {
        var response = await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Generate_WithCoinsAndAProfile_ShouldReturnAPersonalizedAnalysis()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(Ok());
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device, profile: Profile()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DecisionResponseDto>();
        body!.Recommendation.Should().Be("Option A");
        body.Personalized.Should().BeTrue();
        body.Balance.Should().Be(4, "one coin was spent from the five seeded");
    }

    [Fact]
    public async Task Generate_WithoutAProfile_ShouldStillAnswerAndCostACoin()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(Ok());
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<DecisionResponseDto>())!.Balance.Should().Be(4);
    }

    [Fact]
    public async Task Generate_WithNoCoins_ShouldReturnPaymentRequiredAndNotCallGemini()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();   // never seeded, so the balance is zero

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        await gemini.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Generate_WhenGeminiIsUnavailable_ShouldRefundTheCoin()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new DecisionUnavailableException("down"));
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}");
        balance!.Balance.Should().Be(5, "a server-side failure must not cost the person a coin");
    }

    [Fact]
    public async Task Generate_WhenTheModelReturnsGarbage_ShouldRefundAndReturnBadGateway()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new MalformedAdvisorResponseException("nope"));
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5);
    }

    [Fact]
    public async Task Generate_WithAnUnknownPersona_ShouldReturnBadRequestWithoutSpending()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device, persona: "no-such-persona"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5);
        await gemini.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default, default);
    }

    [Theory]
    [InlineData("", "Stay put", 3)]
    [InlineData("Take the job", "", 3)]
    [InlineData("Take the job", "Stay put", 0)]
    [InlineData("Take the job", "Stay put", 6)]
    public async Task Generate_WithInvalidInput_ShouldReturnBadRequestWithoutSpending(string a, string b, int weight)
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions",
            new { deviceId = device, optionA = a, optionB = b, context = (string?)null, persona = "pure-logic", weight, profile = (object?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5);
    }

    [Fact]
    public async Task Generate_OnSuccess_ShouldRecordTheTokenCountsAndPromptVersion()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(Ok());
        var factory = new CoreChoiceAppFactory(gemini);
        var client = factory.CreateClient();
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        await client.PostAsJsonAsync("/api/decisions", Body(device, profile: Profile()));

        // Assert
        var log = await SingleUsageLogAsync(factory);

        log.PromptTokens.Should().Be(100);
        log.OutputTokens.Should().Be(50);
        log.TotalTokens.Should().Be(150);
        log.PersonaId.Should().Be("pure-logic");
        log.PromptVersion.Should().Be(1);
        log.Success.Should().BeTrue();
        log.DeviceHash.Should().NotBe(device.ToString(), "the raw device id must not be stored");
    }

    [Fact]
    public async Task Generate_WhenTheModelReturnsGarbage_ShouldRecordAFailedUsageRow()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new MalformedAdvisorResponseException("nope"));
        var factory = new CoreChoiceAppFactory(gemini);
        var client = factory.CreateClient();
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert — the failed generation is still cost-accounted, at zero tokens.
        var log = await SingleUsageLogAsync(factory);
        log.Success.Should().BeFalse();
        log.TotalTokens.Should().Be(0);
        log.PersonaId.Should().Be("pure-logic");
        log.PromptVersion.Should().Be(1);
    }

    [Fact]
    public async Task Generate_WhenGeminiIsUnavailable_ShouldRecordAFailedUsageRow()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new DecisionUnavailableException("down"));
        var factory = new CoreChoiceAppFactory(gemini);
        var client = factory.CreateClient();
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert
        var log = await SingleUsageLogAsync(factory);
        log.Success.Should().BeFalse();
        log.TotalTokens.Should().Be(0);
        log.PersonaId.Should().Be("pure-logic");
        log.PromptVersion.Should().Be(1);
    }

    [Fact]
    public async Task Generate_WhenTheCallerDisconnectsAfterGeminiAnswers_ShouldStillRefundTheCoin()
    {
        // Arrange — the worst variant from the review: the advisor has already answered (Gemini is
        // paid for) at the moment the caller's connection dies. The fake advisor cancels the
        // client's own token — which the in-memory TestServer propagates onto the server's
        // RequestAborted/`ct` — right before it hands back a result, simulating the disconnect
        // landing between "Gemini answered" and "we finished logging it".
        using var cts = new CancellationTokenSource();
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(_ =>
        {
            cts.Cancel();
            return Task.FromResult(Ok());
        });
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        try
        {
            await client.PostAsJsonAsync("/api/decisions", Body(device), cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected: the caller's own token was cancelled mid-flight.
        }
        catch (HttpRequestException)
        {
            // Some transports surface the cancellation wrapped in this instead.
        }

        // Assert
        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}");
        balance!.Balance.Should().Be(5,
            "a caller disconnect must not burn the coin the refund exists to return");
    }

    [Fact]
    public async Task Generate_WhenTheAdvisorThrowsSomethingUnexpected_ShouldRefundAndNotReturnSuccess()
    {
        // Arrange — neither DecisionUnavailableException nor MalformedAdvisorResponseException: an
        // exception type the catch-all, not the two typed handlers, is responsible for.
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("boom"));
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        HttpResponseMessage? response = null;
        try
        {
            response = await client.PostAsJsonAsync("/api/decisions", Body(device));
        }
        catch (HttpRequestException)
        {
            // An unhandled exception may surface as a broken response rather than a status code,
            // depending on the host; either way it must not be a 2xx.
        }

        // Assert
        if (response is not null)
            response.IsSuccessStatusCode.Should().BeFalse(
                "an unexpected failure must never look like success");

        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5,
            "the catch-all must refund even when it does not know what went wrong");
    }

    [Fact]
    public async Task Generate_WithAProfile_ShouldPassTheProfileAndDilemmaToTheAdvisor()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(Ok());
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        await client.PostAsJsonAsync("/api/decisions", Body(device, profile: Profile()));

        // Assert — the profile actually shaped the system prompt, and the dilemma actually reached
        // the fenced user block, rather than the endpoint quietly ignoring both.
        await gemini.Received(1).AnalyseAsync(
            Arg.Is<string>(s => s.Contains("openness: 80", StringComparison.Ordinal)),
            Arg.Is<string>(u => u.Contains("Take the job", StringComparison.Ordinal)
                && u.Contains("Stay put", StringComparison.Ordinal)),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_WithoutAProfile_ShouldTellTheAdvisorNoProfileExistsButStillPassTheDilemma()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(Ok());
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        await client.PostAsJsonAsync("/api/decisions", Body(device));

        // Assert
        await gemini.Received(1).AnalyseAsync(
            Arg.Is<string>(s => s.Contains("has not taken the personality test", StringComparison.Ordinal)),
            Arg.Is<string>(u => u.Contains("Take the job", StringComparison.Ordinal)
                && u.Contains("Stay put", StringComparison.Ordinal)),
            false,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(150)]
    [InlineData(-1)]
    public async Task Generate_WithAnOutOfRangeTraitScore_ShouldReturnBadRequestWithoutSpending(int trait)
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);
        var badProfile = new { openness = trait, conscientiousness = 50, extraversion = 30, agreeableness = 60, neuroticism = 40 };

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions", Body(device, profile: badProfile));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5);
        await gemini.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Generate_WithAnOversizedOption_ShouldReturnBadRequestWithoutSpending()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);
        var oversized = new string('a', Dilemma.MaxOptionLength + 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/decisions",
            new { deviceId = device, optionA = oversized, optionB = "Stay put", context = (string?)null, persona = "pure-logic", weight = 3, profile = (object?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5);
        await gemini.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Generate_WithAnUnparseableBody_ShouldReturnBadRequestWithoutSpending()
    {
        // Arrange
        var gemini = Substitute.For<IGeminiClient>();
        var (client, _) = await BuildAsync(gemini);
        var device = Guid.NewGuid();
        await SeedAsync(client, device);

        // Act
        var response = await client.PostAsync("/api/decisions",
            new StringContent("{ this is not json", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}"))!.Balance.Should().Be(5);
        await gemini.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default, default);
    }

    private static async Task<Server.Data.UsageLog> SingleUsageLogAsync(CoreChoiceAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Server.Data.ServerDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.UsageLogs);
    }

    private sealed record DecisionResponseDto(
        string Recommendation, int Confidence, IReadOnlyList<string> Reasoning,
        object OptionA, object OptionB, string PersonalityNote, bool Personalized, int Balance);

    private sealed record BalanceDto(Guid DeviceId, int Balance);
}
