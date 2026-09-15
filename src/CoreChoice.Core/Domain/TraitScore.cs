namespace CoreChoice.Domain;

/// <summary>The five factors of the Big Five model.</summary>
public enum Trait
{
    Openness,
    Conscientiousness,
    Extraversion,
    Agreeableness,
    Neuroticism,
}

/// <summary>
/// One trait's score, normalized to 0-100. A struct rather than a bare int so an out-of-range
/// value cannot travel: a personality score that is quietly wrong still looks entirely plausible,
/// which is exactly the kind of bug that survives to production.
/// </summary>
public readonly record struct TraitScore
{
    private TraitScore(int value) => Value = value;

    public int Value { get; }

    public static TraitScore From(int value) =>
        value is < 0 or > 100
            ? throw new ArgumentOutOfRangeException(nameof(value), value, "Trait scores run from 0 to 100.")
            : new TraitScore(value);

    /// <summary>Coarse band used in prompt text, where "high agreeableness" reads better than "81".</summary>
    public string Band => Value switch
    {
        <= 32 => "low",
        <= 66 => "moderate",
        _ => "high",
    };

    public override string ToString() => Value.ToString();
}
