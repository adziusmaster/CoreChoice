namespace CoreChoice.Domain;

/// <summary>
/// The two options a person is weighing, with optional free-text context.
///
/// The length caps live here rather than in endpoint validation because this text becomes prompt
/// input: unbounded text is an unbounded bill, and a rule an endpoint can forget is a rule that
/// eventually gets forgotten.
/// </summary>
public sealed record Dilemma
{
    public const int MaxOptionLength = 500;
    public const int MaxContextLength = 1000;

    private Dilemma(string optionA, string optionB, string? context)
    {
        OptionA = optionA;
        OptionB = optionB;
        Context = context;
    }

    public string OptionA { get; }
    public string OptionB { get; }
    public string? Context { get; }

    public static Dilemma Create(string optionA, string optionB, string? context = null)
    {
        var a = Require(optionA, nameof(optionA));
        var b = Require(optionB, nameof(optionB));

        var trimmedContext = context?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedContext))
            trimmedContext = null;
        else if (trimmedContext.Length > MaxContextLength)
            throw new ArgumentException(
                $"Context may be at most {MaxContextLength} characters.", nameof(context));

        return new Dilemma(a, b, trimmedContext);
    }

    private static string Require(string value, string paramName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("An option cannot be empty.", paramName);
        if (trimmed.Length > MaxOptionLength)
            throw new ArgumentException(
                $"An option may be at most {MaxOptionLength} characters.", paramName);
        return trimmed;
    }
}
