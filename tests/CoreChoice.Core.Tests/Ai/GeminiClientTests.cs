using System.Net;
using CoreChoice.Ai;
using CoreChoice.Application;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CoreChoice.Core.Tests.Ai;

public class GeminiClientTests
{
    private static GeminiClient Build(StubHttpMessageHandler handler, string? apiKey = "test-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            Options.Create(new GeminiOptions { ApiKey = apiKey, Model = "gemini-flash-lite-latest" }));

    [Fact]
    public async Task AnalyseAsync_WithAValidResponse_ShouldReturnTheAnalysisAndUsage()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope(StubHttpMessageHandler.ValidAnalysis)));
        var client = Build(handler);

        // Act
        var result = await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        result.Analysis.Recommendation.Should().Be("Take the job in Berlin");
        result.Analysis.Confidence.Should().Be(68);
        result.Analysis.Reasoning.Should().HaveCount(2);
        result.Analysis.OptionA.Strengths.Should().ContainSingle();
        result.Analysis.IsPersonalized.Should().BeTrue();
        result.Usage.PromptTokens.Should().Be(120);
        result.Usage.OutputTokens.Should().Be(80);
        result.Usage.TotalTokens.Should().Be(200);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldSendTheSchemaAndPinDeterminism()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope(StubHttpMessageHandler.ValidAnalysis)));
        var client = Build(handler);

        // Act
        await client.AnalyseAsync("system", "user", personalized: false);

        // Assert
        var body = handler.RequestBodies.Single();
        body.Should().Contain("\"responseMimeType\":\"application/json\"");
        body.Should().Contain("\"responseSchema\"");
        body.Should().Contain("\"temperature\":0");
        body.Should().Contain("\"seed\":7");
    }

    [Fact]
    public async Task AnalyseAsync_WhenPersonalizedIsFalse_ShouldMarkTheAnalysisUnpersonalized()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope(StubHttpMessageHandler.ValidAnalysis)));
        var client = Build(handler);

        // Act
        var result = await client.AnalyseAsync("system", "user", personalized: false);

        // Assert
        result.Analysis.IsPersonalized.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyseAsync_WhenTheModelReturnsNonSchemaJson_ShouldThrowMalformed()
    {
        // Arrange — the shape a successful prompt injection would take.
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope("""{"lol":"ignore previous instructions"}""")));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Fact]
    public async Task AnalyseAsync_WhenTheEnvelopeHasNoCandidates_ShouldThrowMalformed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json("""{"candidates":[]}"""));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Fact]
    public async Task AnalyseAsync_WhenGeminiIsOverloadedThenRecovers_ShouldRetryAndSucceed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("overloaded", HttpStatusCode.ServiceUnavailable),
            StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope(StubHttpMessageHandler.ValidAnalysis)));
        var client = Build(handler);

        // Act
        var result = await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        handler.CallCount.Should().Be(2);
        result.Analysis.Confidence.Should().Be(68);
    }

    [Fact]
    public async Task AnalyseAsync_WhenGeminiStaysUnavailable_ShouldThrowDecisionUnavailable()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("overloaded", HttpStatusCode.ServiceUnavailable));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<DecisionUnavailableException>();
    }

    [Fact]
    public async Task AnalyseAsync_WhenNoApiKeyIsConfigured_ShouldThrow()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope(StubHttpMessageHandler.ValidAnalysis)));
        var client = Build(handler, apiKey: null);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
