using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// Builds the one piece of local commentary on the result screen: which trait pulls hardest away
/// from the middle, which trait pulls hardest the other way, and what that pairing tends to cost.
///
/// This runs entirely against the profile already sitting on the phone — no network call, no
/// server round trip — because the result screen has to render with no signal at all. The test
/// result is free and unconditional; that promise would break the moment the one sentence that
/// makes the result feel earned depended on a request that could fail.
///
/// Deterministic: the same five scores always produce the same note, since the only inputs are
/// the scores themselves and fixed, hand-written phrase tables below.
/// </summary>
public static class ProfileNoteComposer
{
    /// <summary>
    /// A trait counts as pulling away from the middle once it is off-centre by this much — the
    /// same 15-point distance that separates "Moderate" from "Low"/"High" in <see cref="TraitDisplayBand"/>.
    /// </summary>
    private const int SalientDeviation = 15;

    private static readonly Trait[] AllTraits =
    [
        Trait.Openness, Trait.Conscientiousness, Trait.Extraversion, Trait.Agreeableness, Trait.Neuroticism,
    ];

    public static string Compose(OceanProfile profile)
    {
        var deviations = AllTraits
            .Select(t => (Trait: t, Dev: profile[t].Value - 50))
            .OrderByDescending(d => Math.Abs(d.Dev))
            .ToArray();

        var primary = deviations[0];

        if (Math.Abs(primary.Dev) < SalientDeviation)
        {
            return "No trait here pulls far from the middle. That is an even profile, which tends "
                + "to make you adaptable rather than predictable in any one direction.";
        }

        var primaryIsHigh = primary.Dev > 0;
        var lead = primaryIsHigh ? HighLead[primary.Trait] : LowLead[primary.Trait];

        var secondary = deviations
            .Skip(1)
            .Where(d => Math.Sign(d.Dev) == (primaryIsHigh ? -1 : 1) && Math.Abs(d.Dev) >= SalientDeviation)
            .FirstOrDefault();

        if (secondary == default)
            return $"{lead}. The rest of your profile stays close to the middle.";

        var secondaryIsHigh = secondary.Dev > 0;
        var trail = secondaryIsHigh ? HighTrail[secondary.Trait] : LowTrail[secondary.Trait];
        var consequence = primaryIsHigh ? HighConsequence[primary.Trait] : LowConsequence[primary.Trait];

        return $"{lead}, {trail}. {consequence}";
    }

    // Leads open the sentence: "{Lead}, {trail}." — used for whichever trait deviates furthest
    // from the middle, in the direction it actually deviates.
    private static readonly Dictionary<Trait, string> HighLead = new()
    {
        [Trait.Openness] = "You are drawn to what is new and possible",
        [Trait.Conscientiousness] = "You are drawn to finishing what you start",
        [Trait.Extraversion] = "You are energized by people and noise",
        [Trait.Agreeableness] = "You are quick to give others the benefit of the doubt",
        [Trait.Neuroticism] = "You feel things sharply",
    };

    private static readonly Dictionary<Trait, string> LowLead = new()
    {
        [Trait.Openness] = "You favor the tried and the already-understood",
        [Trait.Conscientiousness] = "You start more than you finish",
        [Trait.Extraversion] = "You keep your own company",
        [Trait.Agreeableness] = "You say what you think, even when it costs you the room",
        [Trait.Neuroticism] = "You stay steady under pressure",
    };

    // Trails close the sentence, attached after the lead — used for the trait pulling the
    // opposite way from the lead trait, phrased as what it costs or neglects.
    private static readonly Dictionary<Trait, string> HighTrail = new()
    {
        [Trait.Openness] = "and give real weight to ideas that haven't proven themselves yet",
        [Trait.Conscientiousness] = "and less comfortable leaving things loose or unresolved",
        [Trait.Extraversion] = "and less at ease with long stretches of solitude",
        [Trait.Agreeableness] = "and slower to say the blunt thing a situation actually calls for",
        [Trait.Neuroticism] = "and less able to shrug off a setback and move on",
    };

    private static readonly Dictionary<Trait, string> LowTrail = new()
    {
        [Trait.Openness] = "and less drawn to the untested or the strange",
        [Trait.Conscientiousness] = "and less drawn to the systems that make new things survive",
        [Trait.Extraversion] = "and less inclined to seek out a room full of people",
        [Trait.Agreeableness] = "and less inclined to spare someone's feelings to keep the peace",
        [Trait.Neuroticism] = "and less quick to notice when something is actually wrong",
    };

    // The closing consequence sentence, keyed only to the lead trait: the two clauses above
    // already name the specific pairing, so this line draws out the further implication rather
    // than repeating it.
    private static readonly Dictionary<Trait, string> HighConsequence = new()
    {
        [Trait.Openness] = "That combination tends to produce a great many beginnings.",
        [Trait.Conscientiousness] = "That combination tends to produce work that outlasts the person who made it.",
        [Trait.Extraversion] = "That combination tends to fill a room faster than it settles one.",
        [Trait.Agreeableness] = "That combination tends to keep the peace at the cost of saying the hard thing.",
        [Trait.Neuroticism] = "That combination tends to turn small setbacks into long recoveries.",
    };

    private static readonly Dictionary<Trait, string> LowConsequence = new()
    {
        [Trait.Openness] = "That combination tends to produce steady ground and few surprises.",
        [Trait.Conscientiousness] = "That combination tends to leave good ideas half-built.",
        [Trait.Extraversion] = "That combination tends to produce depth in a few places rather than reach across many.",
        [Trait.Agreeableness] = "That combination tends to clear the air faster than it wins people over.",
        [Trait.Neuroticism] = "That combination tends to miss the early signal that something needs attention.",
    };
}
