namespace CoreChoice.Domain;

/// <summary>
/// A complete Big Five profile, or <see cref="None"/> when the person has not taken the test.
///
/// The absent case is modelled here rather than as a null at each call site, so "is this analysis
/// personalized?" has exactly one answer in the codebase instead of one per consumer.
/// </summary>
public sealed record OceanProfile(
    TraitScore Openness,
    TraitScore Conscientiousness,
    TraitScore Extraversion,
    TraitScore Agreeableness,
    TraitScore Neuroticism)
{
    /// <summary>The not-yet-tested state. Every score reads zero and <see cref="IsPresent"/> is false.</summary>
    public static OceanProfile None { get; } = new(
        TraitScore.From(0), TraitScore.From(0), TraitScore.From(0),
        TraitScore.From(0), TraitScore.From(0))
    { IsPresent = false };

    /// <summary>False only for <see cref="None"/>. Init-only so it cannot be flipped after construction.</summary>
    public bool IsPresent { get; private init; } = true;

    public TraitScore this[Trait trait] => trait switch
    {
        Trait.Openness => Openness,
        Trait.Conscientiousness => Conscientiousness,
        Trait.Extraversion => Extraversion,
        Trait.Agreeableness => Agreeableness,
        Trait.Neuroticism => Neuroticism,
        _ => throw new ArgumentOutOfRangeException(nameof(trait), trait, null),
    };
}
