using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreChoice.Application;
using CoreChoice.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreChoice.Ai;

public sealed class GeminiClient(
    HttpClient http,
    IOptions<GeminiOptions> options,
    ILogger<GeminiClient>? logger = null) : IGeminiClient
{
    private readonly GeminiOptions _options = options.Value;

    public async Task<DecisionResult> AnalyseAsync(
        string systemPrompt, string userBlock, bool personalized, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Gemini API key is not configured.");

        var request = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userBlock } } } },
            generationConfig = new
            {
                // Both are needed. Gemini picks a random seed unless given one, so temperature 0
                // alone still lets the same dilemma come back shaped differently each time — which
                // makes a bug report describing "the answer was wrong" impossible to reproduce.
                temperature = 0.0,
                seed = 7,
                responseMimeType = "application/json",
                responseSchema = DecisionSchema.Value,
            },
        };

        var json = await SendWithRetryAsync($"v1beta/models/{_options.Model}:generateContent", request, ct);
        return Read(json, personalized);
    }

    private DecisionResult Read(string responseJson, bool personalized)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(responseJson);
        }
        catch (JsonException ex)
        {
            throw new MalformedAdvisorResponseException($"envelope was not JSON: {ex.Message}");
        }

        using (doc)
        {
            var usage = ReadUsage(doc.RootElement);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0)
                throw new MalformedAdvisorResponseException("no candidates returned");

            var candidate = candidates[0];

            if (!candidate.TryGetProperty("content", out var content))
                throw new MalformedAdvisorResponseException(
                    "candidate has no content (this is what a safety-blocked candidate looks like)");

            if (!content.TryGetProperty("parts", out var parts))
                throw new MalformedAdvisorResponseException("content has no parts");

            if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
                throw new MalformedAdvisorResponseException("parts was empty");

            if (!parts[0].TryGetProperty("text", out var textElement))
                throw new MalformedAdvisorResponseException("parts[0] has no text");

            var text = textElement.GetString();

            if (string.IsNullOrWhiteSpace(text))
                throw new MalformedAdvisorResponseException("empty candidate text");

            Payload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<Payload>(text);
            }
            catch (JsonException ex)
            {
                throw new MalformedAdvisorResponseException($"analysis was not JSON: {ex.Message}");
            }

            if (payload?.Recommendation is null or "" || payload.OptionA is null || payload.OptionB is null)
                throw new MalformedAdvisorResponseException("analysis did not match the schema");

            var analysis = new DecisionAnalysis(
                payload.Recommendation.Trim(),
                // The wire value is a double (see Payload.Confidence) because a real Gemini response
                // can emit "70.0" for a whole-number confidence; DecisionAnalysis.Confidence stays an
                // int, so clamp first (guarding against an out-of-range or fractional value from a
                // model that ignores the schema) and then round to the nearest whole point.
                (int)Math.Round(Math.Clamp(payload.Confidence, 0, 100), MidpointRounding.AwayFromZero),
                Clean(payload.Reasoning),
                new OptionAssessment("A", Clean(payload.OptionA.Strengths), Clean(payload.OptionA.Risks)),
                new OptionAssessment("B", Clean(payload.OptionB.Strengths), Clean(payload.OptionB.Risks)),
                payload.PersonalityNote?.Trim() ?? string.Empty,
                personalized);

            return new DecisionResult(analysis, usage);
        }
    }

    private static IReadOnlyList<string> Clean(string[]? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();

    private static TokenUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage))
            return TokenUsage.Empty;

        static int Read(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)
                ? n : 0;

        return new TokenUsage(
            Read(usage, "promptTokenCount"),
            Read(usage, "candidatesTokenCount"),
            Read(usage, "totalTokenCount"));
    }

    private async Task<string> SendWithRetryAsync(string url, object request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request),
            };
            httpRequest.Headers.Add("X-goog-api-key", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= _options.MaxAttempts)
                    throw new DecisionUnavailableException("The advisor could not be reached.", ex);
                await BackoffAsync(attempt, ct);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return await response.Content.ReadAsStringAsync(ct);
                    }
                    catch (HttpRequestException ex)
                    {
                        if (attempt >= _options.MaxAttempts)
                            throw new DecisionUnavailableException("The advisor could not be reached.", ex);
                        await BackoffAsync(attempt, ct);
                        continue;
                    }
                }

                var retryable = response.StatusCode
                    is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.InternalServerError;

                if (!retryable || attempt >= _options.MaxAttempts)
                {
                    logger?.LogWarning("Gemini returned {Status} after {Attempts} attempt(s).",
                        (int)response.StatusCode, attempt);
                    throw new DecisionUnavailableException(
                        $"The advisor returned {(int)response.StatusCode}.");
                }
            }

            await BackoffAsync(attempt, ct);
        }
    }

    private static Task BackoffAsync(int attempt, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), ct);

    private sealed class Payload
    {
        [JsonPropertyName("recommendation")] public string? Recommendation { get; set; }
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
        [JsonPropertyName("reasoning")] public string[]? Reasoning { get; set; }
        [JsonPropertyName("optionA")] public Side? OptionA { get; set; }
        [JsonPropertyName("optionB")] public Side? OptionB { get; set; }
        [JsonPropertyName("personalityNote")] public string? PersonalityNote { get; set; }

        internal sealed class Side
        {
            [JsonPropertyName("strengths")] public string[]? Strengths { get; set; }
            [JsonPropertyName("risks")] public string[]? Risks { get; set; }
        }
    }
}
