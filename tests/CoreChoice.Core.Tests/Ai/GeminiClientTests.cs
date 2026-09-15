using System.Net;
using CoreChoice.Ai;
using CoreChoice.Application;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CoreChoice.Core.Tests.Ai;

/// <summary>
/// A response body that fails partway through being read, the way a dropped connection does.
/// In-memory StringContent cannot reproduce this, which is why the retry-on-body-failure path
/// had no coverage.
/// </summary>
internal sealed class FailingHttpContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        throw new HttpRequestException("The connection was closed while reading the response body.");

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}

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
    public async Task AnalyseAsync_WhenCandidateHasNoContent_ShouldThrowMalformed()
    {
        // Arrange — this is what a candidate blocked on safety grounds looks like: no
        // `content` property at all, just a finishReason.
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("""{"candidates":[{"finishReason":"SAFETY"}]}"""));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Fact]
    public async Task AnalyseAsync_WhenContentHasNoParts_ShouldThrowMalformed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("""{"candidates":[{"content":{}}]}"""));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Fact]
    public async Task AnalyseAsync_WhenPartsIsEmpty_ShouldThrowMalformed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("""{"candidates":[{"content":{"parts":[]}}]}"""));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Fact]
    public async Task AnalyseAsync_WhenPartHasNoText_ShouldThrowMalformed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json("""{"candidates":[{"content":{"parts":[{}]}}]}"""));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<MalformedAdvisorResponseException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task AnalyseAsync_WhenGeminiReturnsANonRetryableStatus_ShouldThrowAfterExactlyOneAttempt(
        HttpStatusCode status)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json("denied", status));
        var client = Build(handler);

        // Act
        Func<Task> act = async () => await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        await act.Should().ThrowAsync<DecisionUnavailableException>();
        handler.CallCount.Should().Be(1);
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

    [Fact]
    public async Task AnalyseAsync_WhenResponseBodyFailsWhileBeingRead_ShouldRetryAndSucceed()
    {
        // Arrange — a first response with headers that succeed (200) but body that fails mid-read,
        // followed by a valid response. The retry handling must catch the body failure, backoff, and retry.
        var handler = new StubHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingHttpContent() },
            () => StubHttpMessageHandler.Json(StubHttpMessageHandler.Envelope(StubHttpMessageHandler.ValidAnalysis)));
        var client = Build(handler);

        // Act
        var result = await client.AnalyseAsync("system", "user", personalized: true);

        // Assert
        handler.CallCount.Should().Be(2);
        result.Analysis.Recommendation.Should().Be("Take the job in Berlin");
        result.Analysis.Confidence.Should().Be(68);
    }
}
