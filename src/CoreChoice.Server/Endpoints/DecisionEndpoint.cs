using CoreChoice.Ai;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoreChoice.Server.Endpoints;

internal static class DecisionEndpoint
{
    public static async Task<IResult> Generate(
        GenerateDecisionRequest request,
        HttpContext http,
        ICoinStore coins,
        IPromptStore prompts,
        IGeminiClient gemini,
        IClientIpHasher hasher,
        IDbContextFactory<ServerDbContext> dbFactory,
        IOptions<CoinOptions> coinOptions,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var price = coinOptions.Value.AnalysisPrice;

        // ---- 1. Validate before spending anything -------------------------------------------
        if (request.DeviceId == Guid.Empty)
            return Results.BadRequest(new { error = "deviceId is required." });

        if (!PersonaId.TryFrom(request.Persona, out var persona))
            return Results.BadRequest(new { error = "persona is not a valid identifier." });

        Dilemma dilemma;
        DecisionWeight weight;
        OceanProfile profile;
        try
        {
            dilemma = Dilemma.Create(request.OptionA, request.OptionB, request.Context);
            weight = DecisionWeight.From(request.Weight);
            profile = MapProfile(request.Profile);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        // The persona must exist before the coin goes: an unknown persona is the caller's mistake,
        // and charging for it would be charging for our own 400.
        var prompt = await prompts.GetActivePromptAsync(persona, ct);
        if (prompt is null)
            return Results.BadRequest(new { error = $"Unknown persona '{persona.Value}'." });

        // ---- 2. Spend ------------------------------------------------------------------------
        if (!await coins.TrySpendAsync(request.DeviceId, price, ct))
        {
            var balance = await coins.GetBalanceAsync(request.DeviceId, ct);
            return Results.Json(
                new { error = "Not enough coins.", required = price, balance },
                statusCode: StatusCodes.Status402PaymentRequired);
        }

        // ---- 3. Everything from here refunds on failure --------------------------------------
        var deviceHash = hasher.HashDevice(request.DeviceId);

        try
        {
            var assembled = PromptAssembler.Assemble(prompt.Template, profile, weight, dilemma);

            var result = await gemini.AnalyseAsync(
                assembled.SystemPrompt, assembled.UserBlock, assembled.Personalized, ct);

            await LogUsageAsync(dbFactory, deviceHash, persona, prompt.Version, weight,
                assembled.Personalized, result.Usage, success: true, ct);

            var balance = await coins.GetBalanceAsync(request.DeviceId, ct);

            return Results.Ok(new DecisionResponse(
                result.Analysis.Recommendation,
                result.Analysis.Confidence,
                result.Analysis.Reasoning,
                new OptionAssessmentDto(result.Analysis.OptionA.Strengths, result.Analysis.OptionA.Risks),
                new OptionAssessmentDto(result.Analysis.OptionB.Strengths, result.Analysis.OptionB.Risks),
                result.Analysis.PersonalityNote,
                result.Analysis.IsPersonalized,
                balance));
        }
        catch (MalformedAdvisorResponseException ex)
        {
            await coins.RefundAsync(request.DeviceId, price, ct);
            await LogUsageAsync(dbFactory, deviceHash, persona, prompt.Version, weight,
                profile.IsPresent, TokenUsage.Empty, success: false, ct);

            // Logged with the prompt version on purpose: an unparseable response is the shape a
            // successful injection takes, and the version is the first thing to check.
            logger.LogWarning(ex, "Malformed advisor response for persona {Persona} prompt v{Version}.",
                persona.Value, prompt.Version);

            return Results.Json(new { error = "The advisor returned an unusable response." },
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is DecisionUnavailableException or OperationCanceledException or HttpRequestException)
        {
            await coins.RefundAsync(request.DeviceId, price, ct);
            await LogUsageAsync(dbFactory, deviceHash, persona, prompt.Version, weight,
                profile.IsPresent, TokenUsage.Empty, success: false, ct);

            return Results.Json(new { error = "The advisor is temporarily unavailable. Please try again." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch
        {
            // Nothing is allowed to keep the coin. An unexpected failure is still our failure.
            await coins.RefundAsync(request.DeviceId, price, ct);
            throw;
        }
    }

    private static OceanProfile MapProfile(OceanProfileDto? dto) =>
        dto is null
            ? OceanProfile.None
            : new OceanProfile(
                TraitScore.From(dto.Openness),
                TraitScore.From(dto.Conscientiousness),
                TraitScore.From(dto.Extraversion),
                TraitScore.From(dto.Agreeableness),
                TraitScore.From(dto.Neuroticism));

    private static async Task LogUsageAsync(
        IDbContextFactory<ServerDbContext> dbFactory,
        string deviceHash,
        PersonaId persona,
        int promptVersion,
        DecisionWeight weight,
        bool personalized,
        TokenUsage usage,
        bool success,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.UsageLogs.Add(new UsageLog
        {
            DeviceHash = deviceHash,
            PersonaId = persona.Value,
            PromptVersion = promptVersion,
            Weight = weight.Value,
            Personalized = personalized,
            PromptTokens = usage.PromptTokens,
            OutputTokens = usage.OutputTokens,
            TotalTokens = usage.TotalTokens,
            Success = success,
            At = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
