using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// The three directions a trait can sit in for the purpose of the per-trait passages below. This
/// deliberately collapses <see cref="TraitDisplayBand"/>'s five levels ("Very low" .. "Very high")
/// down to three: the row already shows the finer adjective, so a passage only needs to carry the
/// meaning for "low", "moderate", or "high" — <c>Very low</c> reads the same passage as
/// <c>Low</c>, and <c>Very high</c> the same as <c>High</c>.
/// </summary>
public enum TraitLevel
{
    Low,
    Moderate,
    High,
}

/// <summary>
/// Builds the "how you decide" passage for one trait — five traits, three directions each, fifteen
/// hand-written passages in total. Like <see cref="ProfileNoteComposer"/>, this runs entirely
/// against the profile already on the phone: no network call, no AI call, nothing that could fail
/// or cost money per render, because the result screen has to render with no signal at all.
///
/// Each passage describes how the trait shows up when choosing between two things — what it makes
/// easy, what it quietly costs — rather than describing the person in general. None of them are
/// advice or praise: they describe, they do not prescribe.
///
/// Deterministic and MAUI-free by design, same as <see cref="ProfileNoteComposer"/>, so it can be
/// linked into <c>CoreChoice.App.Tests</c> and unit-tested directly.
/// </summary>
public static class ProfileTraitSummaryComposer
{
    public static string Compose(Trait trait, TraitScore score) => Passages[trait][LevelOf(score)];

    private static TraitLevel LevelOf(TraitScore score) => score.DisplayBand() switch
    {
        "Very low" or "Low" => TraitLevel.Low,
        "Moderate" => TraitLevel.Moderate,
        _ => TraitLevel.High, // "High" or "Very high"
    };

    // Fifteen hand-written passages: five traits x three directions. Deliberately not twenty-five —
    // the display band already carries the intensity ("Very high" vs "High"); the passage carries
    // the meaning, and that meaning is the same for the inner and outer step on either side.
    private static readonly Dictionary<Trait, Dictionary<TraitLevel, string>> Passages = new()
    {
        [Trait.Openness] = new()
        {
            [TraitLevel.Low] =
                "Between two options, you lean toward the one with a track record, and a new " +
                "alternative has to earn your attention before you spend it there. That keeps you " +
                "from chasing whatever looks shiny, but it can also mean a genuinely better, " +
                "unfamiliar option never quite registers as a real choice.",
            [TraitLevel.Moderate] =
                "You weigh an unfamiliar option and a proven one on their own merits rather than " +
                "favoring either out of habit, which keeps you open without being restless. The " +
                "trade-off is a slower decision than either a committed traditionalist or a " +
                "committed experimenter would make, since neither side wins by default.",
            [TraitLevel.High] =
                "Given two options, the one nobody has tried yet pulls at you before you have " +
                "finished checking whether it actually solves the problem. That keeps your choices " +
                "wide open, but it also means the sufficient, familiar option can look dull by " +
                "comparison even when it is the better answer.",
        },
        [Trait.Conscientiousness] = new()
        {
            [TraitLevel.Low] =
                "You decide as you go rather than laying the choice out in advance, which keeps you " +
                "moving on decisions that do not need much ceremony. The cost shows up later, when a " +
                "commitment made casually turns out to need follow-through you did not plan for.",
            [TraitLevel.Moderate] =
                "You plan the decisions that seem to warrant it and let the rest happen more loosely, " +
                "judging case by case instead of following one fixed process. That keeps you from " +
                "over-engineering small choices, though the line between what deserves planning and " +
                "what does not moves depending on the day.",
            [TraitLevel.High] =
                "Before committing, you want the criteria settled and the steps after the decision " +
                "already mapped, which is why what you choose tends to actually get carried through. " +
                "The same instinct can turn a genuinely open-ended choice into more organizing than " +
                "the decision itself required.",
        },
        [Trait.Extraversion] = new()
        {
            [TraitLevel.Low] =
                "You work a decision through on your own rather than think it out loud, and by the " +
                "time you commit you have already argued with yourself about it. That gives you a " +
                "choice tested against your own doubts, but it also means you can settle on a " +
                "direction before hearing something that would have changed it.",
            [TraitLevel.Moderate] =
                "You will talk a decision over with someone when that helps and sit with it alone " +
                "when it does not, without a strong pull toward either. That gives you both routes, " +
                "but neither is where you start by instinct, so deciding how to decide can take " +
                "almost as long as deciding.",
            [TraitLevel.High] =
                "You think best with a decision said out loud, tried on someone else before it feels " +
                "real. That gets you a fast read on how a choice will land, but a decision made in a " +
                "quiet room with no one to react to can feel harder to trust than it actually is.",
        },
        [Trait.Agreeableness] = new()
        {
            [TraitLevel.Low] =
                "When two options serve different people, you weigh what you actually want ahead of " +
                "what keeps the room comfortable. That means your choice holds up under your own " +
                "scrutiny even when it is unpopular, but it can also underweight a cost that lands on " +
                "someone else rather than on you.",
            [TraitLevel.Moderate] =
                "You take other people's stake in a decision seriously without automatically " +
                "deferring to it, which keeps a choice from becoming only about keeping the peace. " +
                "What that produces is a compromise more often than a clean answer, sometimes at the " +
                "cost of the better, less comfortable option.",
            [TraitLevel.High] =
                "Choosing between options, you give real weight to how each one lands on the people " +
                "around you, sometimes more than to what you actually want. That makes you easy to " +
                "decide with, but the option that costs you something personally can look reasonable " +
                "simply because it costs someone else less.",
        },
        [Trait.Neuroticism] = new()
        {
            [TraitLevel.Low] =
                "A decision that could go badly does not occupy much space in you once it is made; " +
                "you commit and move on rather than replaying it. That keeps second-guessing from " +
                "eating time a choice does not need, but a genuine warning sign can get the same " +
                "shrug as ordinary noise.",
            [TraitLevel.Moderate] =
                "Some decisions sit with you afterward and some do not, roughly in proportion to what " +
                "was actually at stake. That is a fairly accurate alarm, though it still fires early " +
                "often enough that you will sometimes brace for a consequence that never arrives.",
            [TraitLevel.High] =
                "Once a decision is made, you keep turning it over, alert to what could still go " +
                "wrong even with nothing left to change. That vigilance catches real risk other " +
                "people miss, but a decision that turns out fine can still leave a long tail of " +
                "worry behind it.",
        },
    };
}
