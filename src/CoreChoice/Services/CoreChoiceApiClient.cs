using System.Net.Http.Json;
using System.Text.Json;
using CoreChoice.Application;
using CoreChoice.Domain;
using Microsoft.Extensions.Options;

namespace CoreChoice.Services;

/// <summary>
/// The MAUI app's only route to the backend. Implements the outbound ports the app needs
/// (<see cref="IDecisionClient"/>, <see cref="ICoinLedgerClient"/>, <see cref="IPersonaCatalog"/>)
/// because all three are the same HTTP conversation with the same device id, and translates every
/// failure the server can return into the application exceptions the view models already
/// understand — a screen should never see a bare <see cref="HttpRequestException"/> or status code.
/// </summary>
internal sealed class CoreChoiceApiClient(HttpClient http, IDeviceIdentity deviceIdentity, IOptions<ApiOptions> options)
    : IDecisionClient, ICoinLedgerClient, IPersonaCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CoinBalance> GetBalanceAsync(CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Get, $"api/coins/{deviceId}", body: null, ct);
        var dto = await ReadAsync<BalanceDto>(response, ct);
        return new CoinBalance(dto.Balance);
    }

    public async Task<CoinBalance> EnsureSeededAsync(CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Post, "api/coins/ensure", new { deviceId }, ct);
        var dto = await ReadAsync<BalanceDto>(response, ct);
        return new CoinBalance(dto.Balance);
    }

    public async Task<GrantResult> ClaimProfileGrantAsync(CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Post, "api/coins/profile-grant", new { deviceId }, ct);
        var dto = await ReadAsync<GrantDto>(response, ct);
        return new GrantResult(dto.Granted, dto.Balance, dto.Reason);
    }

    /// <summary>
    /// Calls the not-yet-existing <c>POST /api/billing/redeem</c>. Today this always fails — the
    /// server has no such route — which surfaces as an ordinary <see cref="HttpRequestException"/>
    /// (404) that <c>CoinsViewModel.BuyAsync</c> already treats as "purchase not completed,
    /// nothing consumed". Wired now so the only change needed once the endpoint ships is on the
    /// server, not here.
    /// </summary>
    public async Task<GrantResult> RedeemPurchaseAsync(PurchaseTicket ticket, CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendAsync(HttpMethod.Post, "api/billing/redeem", new
        {
            deviceId,
            productId = ticket.ProductId,
            purchaseToken = ticket.PurchaseToken,
        }, ct);
        var dto = await ReadAsync<GrantDto>(response, ct);
        return new GrantResult(dto.Granted, dto.Balance, dto.Reason);
    }

    /// <summary>
    /// Calls <c>POST /api/promo/redeem</c>. Redemption outcomes are expected results the caller
    /// must tell apart, not exceptions: a revoked, expired, unknown or already-redeemed code is a
    /// normal answer from the server, so this reads the status code itself via
    /// <see cref="SendRawAsync"/> instead of going through <see cref="SendAsync"/>'s automatic
    /// throw-on-failure. Anything the server can return that is not one of those expected shapes
    /// (e.g. 429 from the endpoint's own rate limit, or the server being unreachable) still comes
    /// out as the usual exceptions via <see cref="ThrowForFailureAsync"/>.
    /// </summary>
    public async Task<PromoRedemptionResult> RedeemPromoCodeAsync(string code, CancellationToken ct = default)
    {
        var deviceId = await deviceIdentity.GetOrCreateAsync(ct);
        var response = await SendRawAsync(HttpMethod.Post, "api/promo/redeem", new { deviceId, code }, ct);

        switch ((int)response.StatusCode)
        {
            case 200:
                var dto = await ReadAsync<PromoRedeemDto>(response, ct);
                return PromoRedemptionResult.Redeemed(dto.CoinsGranted, dto.Balance);

            case 404:
                return PromoRedemptionResult.Failed(PromoRedemptionOutcome.InvalidCode);

            case 409:
                return PromoRedemptionResult.Failed(PromoRedemptionOutcome.AlreadyRedeemed);

            case 400:
                var error = await ReadErrorFieldAsync(response, ct);
                return error switch
                {
                    "revoked_code" => PromoRedemptionResult.Failed(PromoRedemptionOutcome.RevokedCode),
                    "expired_code" => PromoRedemptionResult.Failed(PromoRedemptionOutcome.ExpiredCode),
                    _ => PromoRedemptionResult.Failed(PromoRedemptionOutcome.Malformed, error),
                };

            default:
                await ThrowForFailureAsync(response, ct);
                // ThrowForFailureAsync always throws for a status code that reaches its default
                // branch (EnsureSuccessStatusCode); this is unreachable but keeps the compiler happy.
                throw new MalformedAdvisorResponseException(
                    $"unexpected status {(int)response.StatusCode} from promo redemption");
        }
    }

    public async Task<IReadOnlyList<PersonaSummary>> GetPersonasAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Get, "api/personas", body: null, ct);
        var dtos = await ReadAsync<List<PersonaDto>>(response, ct);
        return dtos.Select(d => new PersonaSummary(d.Id, d.DisplayName, d.Description)).ToList();
    }

    public async Task<AnalysedDecision> AnalyseAsync(DecisionRequest request, CancellationToken ct = default)
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

        var analysis = new DecisionAnalysis(
            dto.Recommendation,
            dto.Confidence,
            dto.Reasoning,
            new OptionAssessment("A", dto.OptionA.Strengths, dto.OptionA.Risks),
            new OptionAssessment("B", dto.OptionB.Strengths, dto.OptionB.Risks),
            dto.PersonalityNote,
            dto.Personalized);

        return new AnalysedDecision(analysis, dto.Balance);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var response = await SendRawAsync(method, path, body, ct);

        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, ct);

        return response;
    }

    /// <summary>
    /// Sends the request and translates genuine transport failures (unreachable server, timeout)
    /// into <see cref="DecisionUnavailableException"/>, but returns whatever status code the server
    /// answered with rather than throwing for it — <see cref="SendAsync"/> layers that throw-on-
    /// failure behaviour on top for every caller except <see cref="RedeemPromoCodeAsync"/>, which
    /// needs to read expected non-2xx outcomes (404/400/409) itself.
    /// </summary>
    private async Task<HttpResponseMessage> SendRawAsync(
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

        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DecisionUnavailableException("The server could not be reached.", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // The typed client's own Timeout fired (surfaced as TaskCanceledException wrapping a
            // TimeoutException), not the caller's token — a genuine failure, not a cancellation.
            // When ct itself was cancelled (the caller navigated away, etc.) this filter is false
            // and the exception propagates unchanged as the cancellation it is.
            throw new DecisionUnavailableException("The request timed out.", ex);
        }
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

    private static async Task<string?> ReadErrorFieldAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
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
        T? value;
        try
        {
            value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            // A 2xx carrying a body that is not the JSON we expect. The realistic cause on a phone
            // is a captive portal or proxy answering with its own HTML page, not our server.
            throw new MalformedAdvisorResponseException($"the response body was not valid JSON: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            // Same situation, caught earlier: the content type is not JSON at all.
            throw new MalformedAdvisorResponseException($"the response was not JSON: {ex.Message}");
        }

        return value ?? throw new MalformedAdvisorResponseException("the response body was empty or null.");
    }

    private sealed record BalanceDto(Guid DeviceId, int Balance);

    private sealed record GrantDto(Guid DeviceId, int Balance, bool Granted, string? Reason);

    private sealed record PromoRedeemDto(int CoinsGranted, int Balance);

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
