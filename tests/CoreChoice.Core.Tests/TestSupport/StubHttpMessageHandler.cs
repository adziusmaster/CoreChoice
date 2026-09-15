using System.Net;

namespace CoreChoice.Core.Tests;

/// <summary>Returns a queued sequence of responses, and records the requests it was given.</summary>
internal sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private int _index;

    public List<string> RequestBodies { get; } = [];
    public int CallCount => _index;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        var response = responses[Math.Min(_index, responses.Length - 1)];
        _index++;
        return response;
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
