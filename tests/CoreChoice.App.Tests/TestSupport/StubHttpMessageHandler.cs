using System.Net;

namespace CoreChoice.App.Tests;

/// <summary>Returns a queued sequence of responses, and records the requests it was given.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly (HttpStatusCode Status, string Body, string MediaType)[] _snapshotResponses;
    private readonly Func<HttpResponseMessage>[] _factoryResponses;
    private readonly bool _useFactories;
    private int _index;

    public StubHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        // Snapshot each queued response's status/body/media-type up front. A real HttpMessageHandler
        // constructs a brand-new HttpResponseMessage per call; it can never be asked to reuse one the
        // caller has already disposed. Once the queue is "exhausted" we must therefore synthesize a
        // fresh instance rather than hand back (or read from) an object that may already be disposed.
        _snapshotResponses = responses
            .Select(r => (
                r.StatusCode,
                r.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty,
                r.Content?.Headers.ContentType?.MediaType ?? "application/json"))
            .ToArray();

        foreach (var r in responses)
            r.Dispose();

        _factoryResponses = [];
        _useFactories = false;
    }

    /// <summary>Constructor overload for response factories, used when responses contain content that cannot be snapshotted eagerly (e.g., content that throws on serialization).</summary>
    public StubHttpMessageHandler(params Func<HttpResponseMessage>[] responseFactories)
    {
        _factoryResponses = responseFactories;
        _snapshotResponses = [];
        _useFactories = true;
    }

    public List<string> RequestBodies { get; } = [];
    public List<RecordedRequest> Requests { get; } = [];
    public int CallCount => _index;

    /// <summary>The method and URI of a request the stub received, recorded so a route or verb typo in the client is caught by tests instead of only failing against the live server.</summary>
    public sealed record RecordedRequest(HttpMethod Method, Uri? RequestUri);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri));
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_useFactories)
        {
            var factory = _factoryResponses[Math.Min(_index, _factoryResponses.Length - 1)];
            _index++;
            return factory();
        }
        else
        {
            var (status, body, mediaType) = _snapshotResponses[Math.Min(_index, _snapshotResponses.Length - 1)];
            _index++;

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType),
            };
        }
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    /// <summary>A well-formed Gemini envelope wrapping the given analysis JSON.</summary>
    public static string Envelope(string analysisJson, int promptTokens = 120, int outputTokens = 80) =>
        $$"""
        {
          "candidates": [ { "content": { "parts": [ { "text": {{System.Text.Json.JsonSerializer.Serialize(analysisJson)}} } ] } } ],
          "usageMetadata": {
            "promptTokenCount": {{promptTokens}},
            "candidatesTokenCount": {{outputTokens}},
            "totalTokenCount": {{promptTokens + outputTokens}}
          }
        }
        """;

    public const string ValidAnalysis =
        """
        {
          "recommendation": "Take the job in Berlin",
          "confidence": 68,
          "reasoning": ["The upside compounds", "The downside is bounded"],
          "optionA": { "strengths": ["Growth"], "risks": ["Uprooting"] },
          "optionB": { "strengths": ["Stability"], "risks": ["Stagnation"] },
          "personalityNote": "Your high openness favours the unfamiliar option."
        }
        """;
}
