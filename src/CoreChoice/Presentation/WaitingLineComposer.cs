using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// Builds the one line shown on the loading screen while the advisor thinks. Built entirely from
/// the profile already on the phone — no network call, no server round trip — which is exactly
/// why it can appear the instant the request is sent rather than waiting on a reply that has not
/// arrived yet.
///
/// Deliberately unlike <see cref="ProfileNoteComposer"/>: that composer avoids naming a trait
/// outright ("You are drawn to..." rather than "high in openness") because it is read as a
/// considered description of a person. This line is read as a status update, and for a HIGH trait
/// it names the exact thing being weighed — "Weighing your openness against..." — so it names the
/// trait plainly, on purpose.
///
/// For a LOW trait, naming it the same way would be backwards rather than merely blunt: "Weighing
/// your conscientiousness against the pull of walking away" credits someone with the very
/// discipline they are short on, and puts it on the side of the sentence doing the resisting
/// instead of the side doing the pulling. So the direction changes which role each force plays,
/// not just the wording: a low trait IS the everyday pull (<see cref="HighCounterPull"/> already
/// describes it, from the high trait's point of view, as the thing pulling the other way), weighed
/// against what the high pole of that same trait would have valued instead
/// (<see cref="LowCounterPull"/>). "Weighing the pull of walking away while you still can against
/// the work of finishing what you have" is true of someone low in conscientiousness in a way the
/// old, direction-blind sentence never was. With no profile there is nothing to weigh, so it falls
/// back to a neutral line and never invents a trait to fill the gap.
/// </summary>
public static class WaitingLineComposer
{
    private static readonly Trait[] AllTraits =
    [
        Trait.Openness, Trait.Conscientiousness, Trait.Extraversion, Trait.Agreeableness, Trait.Neuroticism,
    ];

    /// <summary>Shown when there is no profile to draw a trait from at all.</summary>
    private const string NeutralLine = "Weighing this from a few different angles.";

    private static readonly Dictionary<Trait, string> TraitName = new()
    {
        [Trait.Openness] = "openness",
        [Trait.Conscientiousness] = "conscientiousness",
        [Trait.Extraversion] = "extraversion",
        [Trait.Agreeableness] = "agreeableness",
        [Trait.Neuroticism] = "neuroticism",
    };

    /// <summary>What a HIGH trait is weighed against: the everyday, competing pull that makes the
    /// trade-off real. This same phrase is reused as the FORCE itself in the low-direction line
    /// below — the pull it describes belongs to the trait's low pole, so a person who scores low
    /// does not resist it, they embody it. Fixed phrases rather than anything drawn from the
    /// dilemma's own free text: a person's two options are unbounded strings, and slotting them
    /// into a sentence risks a grammatically broken line the moment the wording does not fit
    /// ("the pull of Move to Berlin").</summary>
    private static readonly Dictionary<Trait, string> HighCounterPull = new()
    {
        [Trait.Openness] = "the pull of a steady income",
        [Trait.Conscientiousness] = "the pull of walking away while you still can",
        [Trait.Extraversion] = "the pull of a quiet room",
        [Trait.Agreeableness] = "the pull of putting yourself first",
        [Trait.Neuroticism] = "the pull of staying calm about it",
    };

    /// <summary>What a LOW trait's own pull (<see cref="HighCounterPull"/>) is weighed against:
    /// what the trait's high pole would have offered or valued instead. Keyed separately from
    /// <see cref="HighCounterPull"/> because the two lines are not mirror images of the same
    /// pairing — each names a different pair of forces, chosen to be true of that direction.</summary>
    private static readonly Dictionary<Trait, string> LowCounterPull = new()
    {
        [Trait.Openness] = "what a new opportunity might be worth",
        [Trait.Conscientiousness] = "the work of finishing what you have",
        [Trait.Extraversion] = "what a room full of people might give you",
        [Trait.Agreeableness] = "the ease of keeping the peace",
        [Trait.Neuroticism] = "the signal that something is actually wrong",
    };

    public static string Compose(OceanProfile profile)
    {
        if (!profile.IsPresent)
            return NeutralLine;

        var dominant = AllTraits
            .Select(t => (Trait: t, Dev: profile[t].Value - 50))
            .OrderByDescending(d => Math.Abs(d.Dev))
            .First();

        var trait = dominant.Trait;

        // Dev > 0: the trait genuinely IS the pull, so it can be named as "your {trait}" and
        // weighed against the everyday counter-pull. Dev <= 0 (low, or the degenerate all-50
        // case): naming it "your {trait}" would credit the person with a quality they are short
        // on, so the low pole's own pull (HighCounterPull, from the other direction's point of
        // view) becomes the force, weighed against what the high pole would have valued.
        return dominant.Dev > 0
            ? $"Weighing your {TraitName[trait]} against {HighCounterPull[trait]}."
            : $"Weighing {HighCounterPull[trait]} against {LowCounterPull[trait]}.";
    }
}
