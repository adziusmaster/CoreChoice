using CoreChoice.Domain;

namespace CoreChoice.Application;

/// <summary>Everything one analysis needs. A record so adding a field is a compile error at every call site.</summary>
public sealed record DecisionRequest(
    Dilemma Dilemma,
    OceanProfile Profile,
    PersonaId Persona,
    DecisionWeight Weight);

/// <summary>The analysis plus what it cost to produce.</summary>
public sealed record DecisionResult(DecisionAnalysis Analysis, TokenUsage Usage);

/// <summary>
/// Inbound port: turn a dilemma into an analysis. Implemented on the server by the Gemini-backed
/// service, and in the MAUI app by an HTTP adapter that calls this service.
/// </summary>
public interface IDecisionAdvisor
{
    Task<DecisionResult> AnalyseAsync(DecisionRequest request, CancellationToken ct = default);
}
