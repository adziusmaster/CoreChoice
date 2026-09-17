namespace CoreChoice.Server.Endpoints;

/// <summary>
/// The wire shape of a profile: five plain integers. Mapped to the domain at the boundary rather
/// than bound to it, so a malformed payload is a 400 and never an exception raised mid-assembly.
/// </summary>
internal sealed record OceanProfileDto(
    int Openness, int Conscientiousness, int Extraversion, int Agreeableness, int Neuroticism);

internal sealed record GenerateDecisionRequest(
    Guid DeviceId,
    string OptionA,
    string OptionB,
    string? Context,
    string Persona,
    int Weight,
    OceanProfileDto? Profile);

internal sealed record OptionAssessmentDto(IReadOnlyList<string> Strengths, IReadOnlyList<string> Risks);

internal sealed record DecisionResponse(
    string Recommendation,
    int Confidence,
    IReadOnlyList<string> Reasoning,
    OptionAssessmentDto OptionA,
    OptionAssessmentDto OptionB,
    string PersonalityNote,
    bool Personalized,
    int Balance);

internal sealed record EnsureCoinsRequest(Guid DeviceId);

internal sealed record ProfileGrantRequest(Guid DeviceId);

internal sealed record BalanceResponse(Guid DeviceId, int Balance);

internal sealed record GrantResponse(Guid DeviceId, int Balance, bool Granted, string? Reason);

internal sealed record PersonaResponse(string Id, string DisplayName, string Description);

internal sealed record RedeemCodeRequest(Guid DeviceId, string Code);

internal sealed record RedeemCodeResponse(int CoinsGranted, int Balance);

internal sealed record CreatePromoRequest(string? Code, int? Coins, int? ExpiresInDays);

internal sealed record PromoResponse(
    string Code,
    int Coins,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Revoked,
    int RedemptionCount);
