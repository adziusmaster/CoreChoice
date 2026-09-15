namespace CoreChoice.Ai;

/// <summary>
/// The response schema every analysis must match.
///
/// This is a security control as much as a formatting one: constrained decoding means a successful
/// prompt injection cannot produce prose in place of the analysis — it produces a response that
/// fails to parse, which is a handled error rather than an escape.
/// </summary>
internal static class DecisionSchema
{
    private static object OptionSchema => new
    {
        type = "OBJECT",
        properties = new
        {
            strengths = new { type = "ARRAY", items = new { type = "STRING" } },
            risks = new { type = "ARRAY", items = new { type = "STRING" } },
        },
        required = new[] { "strengths", "risks" },
    };

    public static object Value => new
    {
        type = "OBJECT",
        properties = new
        {
            recommendation = new { type = "STRING" },
            confidence = new { type = "INTEGER" },
            reasoning = new { type = "ARRAY", items = new { type = "STRING" } },
            optionA = OptionSchema,
            optionB = OptionSchema,
            personalityNote = new { type = "STRING" },
        },
        required = new[] { "recommendation", "confidence", "reasoning", "optionA", "optionB", "personalityNote" },
    };
}
