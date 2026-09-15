using CoreChoice.Application;
using CoreChoice.Domain;

namespace CoreChoice.Ai;

/// <summary>
/// Stands in for Gemini when no API key is configured, so the whole application runs end to end on
/// a laptop with no key and no network. Registered by the composition root, not by tests.
/// </summary>
public sealed class FakeGeminiClient : IGeminiClient
{
    public Task<DecisionResult> AnalyseAsync(
        string systemPrompt, string userBlock, bool personalized, CancellationToken ct = default)
    {
        var analysis = new DecisionAnalysis(
            Recommendation: "Option A",
            Confidence: 60,
            Reasoning: ["This is a stub response because no Gemini key is configured."],
            OptionA: new OptionAssessment("A", ["stubbed strength"], ["stubbed risk"]),
            OptionB: new OptionAssessment("B", ["stubbed strength"], ["stubbed risk"]),
            PersonalityNote: personalized ? "Stubbed personality note." : string.Empty,
            IsPersonalized: personalized);

        return Task.FromResult(new DecisionResult(analysis, TokenUsage.Empty));
    }
}
