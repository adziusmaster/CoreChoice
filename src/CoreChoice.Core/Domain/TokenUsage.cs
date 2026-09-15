namespace CoreChoice.Domain;

/// <summary>
/// What one Gemini call cost in tokens. Returned from the advisor rather than only logged, so the
/// server can persist it as a queryable column — the difference between knowing what the service
/// costs and grepping for it.
/// </summary>
public sealed record TokenUsage(int PromptTokens, int OutputTokens, int TotalTokens)
{
    public static TokenUsage Empty { get; } = new(0, 0, 0);
}
