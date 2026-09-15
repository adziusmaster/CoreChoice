namespace CoreChoice.Domain;

/// <summary>Raised when scoring is attempted before every item has been answered.</summary>
public sealed class IncompleteProfileException(int answered)
    : Exception($"A Big Five profile needs all {IpipItemBank.ItemCount} answers; {answered} were supplied.")
{
    public int Answered { get; } = answered;
}

/// <summary>
/// Turns 50 Likert responses into an <see cref="OceanProfile"/>. Pure: no IO, no clock, no
/// randomness, so it is fully testable and identical on the device and on the server.
/// </summary>
public static class IpipScoring
{
    private const int ItemsPerTrait = 10;
    private const int MinRaw = ItemsPerTrait * IpipItemBank.MinResponse;  // 10
    private const int MaxRaw = ItemsPerTrait * IpipItemBank.MaxResponse;  // 50

    /// <param name="responses">Item number to Likert response (1-5). All 50 items required.</param>
    public static OceanProfile Score(IReadOnlyDictionary<int, int> responses)
    {
        ArgumentNullException.ThrowIfNull(responses);

        foreach (var (number, value) in responses)
        {
            // Validates the number exists; throws ArgumentException if it does not.
            _ = IpipItemBank.ByNumber(number);

            if (value is < IpipItemBank.MinResponse or > IpipItemBank.MaxResponse)
                throw new ArgumentOutOfRangeException(
                    nameof(responses), value,
                    $"Item {number}: responses run from {IpipItemBank.MinResponse} to {IpipItemBank.MaxResponse}.");
        }

        // Completeness is checked by IDENTITY, not by count: a 50-entry submission that omits
        // item 17 and includes a bogus 999 has the right size and the wrong content.
        var answered = IpipItemBank.Items.Count(i => responses.ContainsKey(i.Number));
        if (answered != IpipItemBank.ItemCount)
            throw new IncompleteProfileException(answered);

        return new OceanProfile(
            ScoreTrait(Trait.Openness, responses),
            ScoreTrait(Trait.Conscientiousness, responses),
            ScoreTrait(Trait.Extraversion, responses),
            ScoreTrait(Trait.Agreeableness, responses),
            ScoreTrait(Trait.Neuroticism, responses));
    }

    private static TraitScore ScoreTrait(Trait trait, IReadOnlyDictionary<int, int> responses)
    {
        var raw = 0;
        foreach (var item in IpipItemBank.Items)
        {
            if (item.Trait != trait) continue;

            var response = responses[item.Number];
            // A reverse-keyed item measures the opposite pole: "Keep in the background" answered
            // 5 is evidence AGAINST extraversion, so it contributes 1.
            raw += item.IsReverseKeyed
                ? IpipItemBank.MinResponse + IpipItemBank.MaxResponse - response
                : response;
        }

        // Raw runs 10-50; project onto 0-100.
        var normalized = (int)Math.Round((raw - MinRaw) * 100.0 / (MaxRaw - MinRaw), MidpointRounding.AwayFromZero);
        return TraitScore.From(normalized);
    }
}
