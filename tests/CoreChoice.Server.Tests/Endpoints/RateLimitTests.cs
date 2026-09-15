using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using FluentAssertions;
using NSubstitute;

namespace CoreChoice.Server.Tests.Endpoints;

public class RateLimitTests
{
    [Fact]
    public async Task Personas_WhenTheBudgetIsExhausted_ShouldReturnTooManyRequests()
    {
        // Arrange — the personas budget is the highest, so exceeding it deliberately proves the
        // limiter is wired at all; the tighter policies share the same registration.
        var client = new CoreChoiceAppFactory(Substitute.For<IGeminiClient>()).CreateClient();

        // Act — 60 permitted per minute, so the 61st must be refused.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 61; i++)
            last = await client.GetAsync("/api/personas");

        // Assert
        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Decisions_WhenRateLimited_ShouldNotSpendACoin()
    {
        // Arrange — the substitute MUST be configured. A bare Substitute.For<IGeminiClient>()
        // returns a null Task from AnalyseAsync, which throws inside the handler, hits the
        // refund-and-rethrow path, and leaves the balance at 5 instead of 0 — so the test would
        // fail for a reason that has nothing to do with rate limiting.
        var gemini = Substitute.For<IGeminiClient>();
        gemini.AnalyseAsync(default!, default!, default, default).ReturnsForAnyArgs(
            new CoreChoice.Application.DecisionResult(
                new CoreChoice.Domain.DecisionAnalysis("Go", 60, ["r"],
                    new CoreChoice.Domain.OptionAssessment("A", ["s"], ["k"]),
                    new CoreChoice.Domain.OptionAssessment("B", ["s"], ["k"]),
                    "", false),
                CoreChoice.Domain.TokenUsage.Empty));
        var factory = new CoreChoiceAppFactory(gemini);
        var client = factory.CreateClient();
        var device = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/coins/ensure", new { deviceId = device });

        object Body() => new
        {
            deviceId = device, optionA = "Go", optionB = "Stay",
            context = (string?)null, persona = "pure-logic", weight = 3, profile = (object?)null,
        };

        // Act — the decide budget is 10/minute; the 11th is refused before the handler runs.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/api/decisions", Body());

        // Assert — a refusal at the limiter must never reach the ledger.
        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/coins/{device}");
        balance!.Balance.Should().Be(0, "five coins bought five analyses; the rest were refused");
    }

    private sealed record BalanceDto(Guid DeviceId, int Balance);
}
