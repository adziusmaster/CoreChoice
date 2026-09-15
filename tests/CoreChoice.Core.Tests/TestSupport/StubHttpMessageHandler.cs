using System.Net;

namespace CoreChoice.Core.Tests;

/// <summary>Returns a queued sequence of responses, and records the requests it was given.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly (HttpStatusCode Status, string Body, string MediaType)[] _responses;
    private int _index;

    public StubHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        // Snapshot each queued response's status/body/media-type up front. A real HttpMessageHandler
        // constructs a brand-new HttpResponseMessage per call; it can never be asked to reuse one the
        // caller has already disposed. Once the queue is "exhausted" we must therefore synthesize a
        // fresh instance rather than hand back (or read from) an object that may already be disposed.
        _responses = responses
            .Select(r => (
                r.StatusCode,
                r.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty,
                r.Content?.Headers.ContentType?.MediaType ?? "application/json"))
            .ToArray();

        foreach (var r in responses)
            r.Dispose();
    }

    public List<string> RequestBodies { get; } = [];
    public int CallCount => _index;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        var (status, body, mediaType) = _responses[Math.Min(_index, _responses.Length - 1)];
        _index++;

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType),
        };
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
