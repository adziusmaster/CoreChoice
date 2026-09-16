using System.Net.Http.Json;
using System.Text.Json;
using CoreChoice.Application;
using CoreChoice.Domain;
using Microsoft.Extensions.Options;

namespace CoreChoice.Services;

/// <summary>The picker's contents: names and descriptions only, never the prompt template.</summary>
public sealed record PersonaSummary(string Id, string DisplayName, string Description);

/// <summary>
/// The MAUI app's only route to the backend. Implements both outbound ports the app needs
/// (<see cref="IDecisionAdvisor"/> and <see cref="ICoinLedgerClient"/>) because both are the same
/// HTTP conversation with the same device id, and translates every failure the server can return
/// into the application exceptions the view models already understand — a screen should never see
/// a bare <see cref="HttpRequestException"/> or status code.
/// </summary>
internal sealed class CoreChoiceApiClient(HttpClient http, IDeviceIdentity deviceIdentity, IOptions<ApiOptions> options)
    : IDecisionAdvisor, ICoinLedgerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The balance as last reported by the server, from whichever call last carried one. The
    /// decision endpoint returns the post-spend balance in the same round trip that already paid
    /// for the analysis, so callers do not need a second request just to refresh the number.
    /// </summary>
    public int Balance { get; private set; }

    public async Task<CoinBalance> GetBalanceAsync(CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Get, $"api/coins/{deviceId}", body: null, ct);
        var dto = await ReadAsync<BalanceDto>(response, ct);
        Balance = dto.Balance;
        return new CoinBalance(dto.Balance);
    }

    public async Task<CoinBalance> EnsureSeededAsync(CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Post, "api/coins/ensure", new { deviceId }, ct);
        var dto = await ReadAsync<BalanceDto>(response, ct);
        Balance = dto.Balance;
        return new CoinBalance(dto.Balance);
    }

    public async Task<GrantResult> ClaimProfileGrantAsync(CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Post, "api/coins/profile-grant", new { deviceId }, ct);
        var dto = await ReadAsync<GrantDto>(response, ct);
        Balance = dto.Balance;
        return new GrantResult(dto.Granted, dto.Balance, dto.Reason);
    }

    public async Task<IReadOnlyList<PersonaSummary>> GetPersonasAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Get, "api/personas", body: null, ct);
        var dtos = await ReadAsync<List<PersonaDto>>(response, ct);
        return dtos.Select(d => new PersonaSummary(d.Id, d.DisplayName, d.Description)).ToList();
    }

    public async Task<DecisionResult> AnalyseAsync(DecisionRequest request, CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var profile = request.Profile;

        var body = new
        {
            deviceId,
            optionA = request.Dilemma.OptionA,
            optionB = request.Dilemma.OptionB,
            context = request.Dilemma.Context,
            persona = request.Persona.Value,
            weight = request.Weight.Value,
            // The server decides whether an analysis is personalized, not the client: sending a
            // null profile is what makes the answer unpersonalized, regardless of whether the
            // person has ever taken the test.
            profile = profile.IsPresent
                ? new
                {
                    openness = profile.Openness.Value,
                    conscientiousness = profile.Conscientiousness.Value,
                    extraversion = profile.Extraversion.Value,
                    agreeableness = profile.Agreeableness.Value,
                    neuroticism = profile.Neuroticism.Value,
                }
                : null,
        };

        var response = await SendAsync(HttpMethod.Post, "api/decisions", body, ct);
        var dto = await ReadAsync<DecisionResponseDto>(response, ct);
        Balance = dto.Balance;

        var analysis = new DecisionAnalysis(
            dto.Recommendation,
            dto.Confidence,
            dto.Reasoning,
            new OptionAssessment("A", dto.OptionA.Strengths, dto.OptionA.Risks),
            new OptionAssessment("B", dto.OptionB.Strengths, dto.OptionB.Risks),
            dto.PersonalityNote,
            dto.Personalized);

        // The decision endpoint never reports token usage to the app — that is server-internal
        // accounting — so there is nothing honest to put here but the empty value.
        return new DecisionResult(analysis, TokenUsage.Empty);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        // Defensive: the composition root is expected to set the typed HttpClient's BaseAddress
        // from ApiOptions.BaseUrl, but a client built without that step (e.g. constructed by hand)
        // still works rather than throwing on a relative-URI request.
        var requestUri = http.BaseAddress is null
            ? new Uri(new Uri(options.Value.BaseUrl), path)
            : new Uri(path, UriKind.Relative);
        using var request = new HttpRequestMessage(method, requestUri);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DecisionUnavailableException("The server could not be reached.", ex);
        }

        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, ct);

        return response;
    }

    /// <summary>
    /// Translates the wire failures the backend can return rather than letting them leak: a
    /// screen that sees a bare transport exception cannot tell "you're out of coins" from
    /// "the model is down" from "you're being rate-limited", and shows the same unhelpful error
    /// for all three.
    /// </summary>
    private static async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        switch ((int)response.StatusCode)
        {
            case 402:
                var (required, available) = await ReadCoinShortfallAsync(response, ct);
                throw new InsufficientCoinsException(required, available);

            case 502:
                throw new MalformedAdvisorResponseException(
                    $"the server rejected the advisor's response: {await SafeReadBodyAsync(response, ct)}");

            case 503:
                throw new DecisionUnavailableException("The advisor is temporarily unavailable. Please try again.");

            case 429:
                throw new DecisionUnavailableException(
                    "Too many requests right now. Please wait a moment and try again.");

            default:
                response.EnsureSuccessStatusCode();
                break;
        }
    }

    private static async Task<(int Required, int Available)> ReadCoinShortfallAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var required = doc.RootElement.TryGetProperty("required", out var r) ? r.GetInt32() : 0;
            var balance = doc.RootElement.TryGetProperty("balance", out var b) ? b.GetInt32() : 0;
            return (required, balance);
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return value ?? throw new MalformedAdvisorResponseException("the response body was empty or null.");
    }

    private sealed record BalanceDto(Guid DeviceId, int Balance);

    private sealed record GrantDto(Guid DeviceId, int Balance, bool Granted, string? Reason);

    private sealed record PersonaDto(string Id, string DisplayName, string Description);

    private sealed record OptionAssessmentDto(IReadOnlyList<string> Strengths, IReadOnlyList<string> Risks);

    private sealed record DecisionResponseDto(
        string Recommendation,
        int Confidence,
        IReadOnlyList<string> Reasoning,
        OptionAssessmentDto OptionA,
        OptionAssessmentDto OptionB,
        string PersonalityNote,
        bool Personalized,
        int Balance);
}
