using CoreChoice.Domain;

namespace CoreChoice.Application;

/// <summary>The analysis, plus the balance left after paying for it — both from one round trip.</summary>
public sealed record AnalysedDecision(DecisionAnalysis Analysis, int Balance);

/// <summary>
/// The app's route to the decision endpoint. Distinct from <see cref="IDecisionAdvisor"/>, which
/// describes the server's own Gemini-backed analysis and knows nothing about coins.
/// </summary>
public interface IDecisionClient
{
    Task<AnalysedDecision> AnalyseAsync(DecisionRequest request, CancellationToken ct = default);
}
