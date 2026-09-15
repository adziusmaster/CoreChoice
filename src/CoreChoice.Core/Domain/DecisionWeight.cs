namespace CoreChoice.Domain;

/// <summary>
/// How much the decision matters, 1 to 5. Fed to the prompt so the advisor can match its register:
/// a person choosing a restaurant does not want the analysis a person changing career deserves.
/// </summary>
public readonly record struct DecisionWeight
{
    public const int Min = 1;
    public const int Max = 5;

    private DecisionWeight(int value) => Value = value;

    public int Value { get; }

    public static DecisionWeight From(int value) =>
        value is < Min or > Max
            ? throw new ArgumentOutOfRangeException(nameof(value), value, $"Weight runs from {Min} to {Max}.")
            : new DecisionWeight(value);

    /// <summary>Plain-language stake, injected into the prompt rather than a bare number.</summary>
    public string Description => Value switch
    {
        1 => "a small, easily reversed choice",
        2 => "a minor choice with limited consequences",
        3 => "a meaningful choice worth thinking through",
        4 => "a significant choice that will be hard to undo",
        5 => "a major, life-shaping choice",
        _ => throw new InvalidOperationException($"Unreachable weight {Value}."),
    };

    public override string ToString() => Value.ToString();
}
