using CoreChoice.Application;

namespace CoreChoice.Ai;

public interface IGeminiClient
{
    /// <param name="systemPrompt">The persona's template, already assembled.</param>
    /// <param name="userBlock">The dilemma, already delimited as untrusted data.</param>
    /// <param name="personalized">Whether a real profile went into the prompt.</param>
    Task<DecisionResult> AnalyseAsync(
        string systemPrompt, string userBlock, bool personalized, CancellationToken ct = default);
}
