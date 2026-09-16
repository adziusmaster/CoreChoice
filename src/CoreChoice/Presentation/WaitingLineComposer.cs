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
/// considered description of a person. This line is read as a status update, and the whole point
/// is to name the exact thing being weighed — "Weighing your openness against..." — so it names
/// the trait plainly, on purpose. With no profile there is nothing to weigh, so it falls back to a
/// neutral line and never invents a trait to fill the gap.
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

    /// <summary>What each trait is weighed against — the everyday, competing pull that makes the
    /// trade-off real, independent of which direction the profile actually leans. Fixed phrases
    /// rather than anything drawn from the dilemma's own free text: a person's two options are
    /// unbounded strings, and slotting them into a sentence risks a grammatically broken line the
    /// moment the wording does not fit ("the pull of Move to Berlin").</summary>
    private static readonly Dictionary<Trait, string> CounterPull = new()
    {
        [Trait.Openness] = "the pull of a steady income",
        [Trait.Conscientiousness] = "the pull of walking away while you still can",
        [Trait.Extraversion] = "the pull of a quiet room",
        [Trait.Agreeableness] = "the pull of putting yourself first",
        [Trait.Neuroticism] = "the pull of staying calm about it",
    };

    public static string Compose(OceanProfile profile)
    {
        if (!profile.IsPresent)
            return NeutralLine;

        var dominant = AllTraits
            .Select(t => (Trait: t, Deviation: Math.Abs(profile[t].Value - 50)))
            .OrderByDescending(d => d.Deviation)
            .First()
            .Trait;

        return $"Weighing your {TraitName[dominant]} against {CounterPull[dominant]}.";
    }
}
