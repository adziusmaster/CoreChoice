namespace CoreChoice.Domain;

/// <summary>One option, weighed.</summary>
public sealed record OptionAssessment(
    string Option,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Risks);

/// <summary>
/// The advisor's answer. <paramref name="PersonalityNote"/> is the line that ties the analysis to
/// this particular person; it is what the loading screen's copy is written to pay off, and it is
/// empty for an unpersonalized analysis.
/// </summary>
public sealed record DecisionAnalysis(
    string Recommendation,
    int Confidence,
    IReadOnlyList<string> Reasoning,
    OptionAssessment OptionA,
    OptionAssessment OptionB,
    string PersonalityNote,
    bool IsPersonalized);
