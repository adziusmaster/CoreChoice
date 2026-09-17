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
/// Which of the two tiers a passage belongs to. The distinction is the whole point of this file
/// and it is load-bearing, not decorative: <see cref="Established"/> passages state a finding from
/// a large study at the strength that study actually found, and <see cref="Interpretation"/>
/// passages state that nothing is established and then either read the score as one possible
/// reading or report a fact about people in general with no trait attached.
///
/// The literature review at <c>docs/research/big-five-decision-making.md</c> §11 permits exactly
/// four <see cref="Established"/> cells out of fifteen. Anything else claiming that tier is a bug,
/// and <c>ProfileTraitSummaryComposerTests</c> fails on it by name.
/// </summary>
public enum PassageTier
{
    /// <summary>Backed by a large study, quoted at the strength the study found.</summary>
    Established,

    /// <summary>Not established. One way to read the score, or a fact about people in general.</summary>
    Interpretation,
}

/// <summary>One trait's passage as the profile screen renders it: which trait, which tier, what it says.</summary>
public sealed record TraitPassage(Trait Trait, string TraitName, TraitLevel Level, PassageTier Tier, string Text);

/// <summary>
/// Builds the "how you decide" passage for one trait — five traits, three directions each, fifteen
/// hand-written passages in total. Like <see cref="ProfileNoteComposer"/>, this runs entirely
/// against the profile already on the phone: no network call, no AI call, nothing that could fail
/// or cost money per render, because the result screen has to render with no signal at all.
///
/// <para><b>Every passage here is written against the literature review</b> at
/// <c>docs/research/big-five-decision-making.md</c>, and specifically against its §11 ceiling on
/// what may be asserted. Four cells clear that bar and speak in the second person about the
/// reader: conscientiousness high and low (Steel 2007, r ≈ .63 with procrastination, corrected by
/// Phillips et al. 2016, r = .11 with decision quality), openness high and its minimal mirror at
/// low (Highhouse et al. 2022, ρ = .30 with risk propensity, N = 69,125, Big Five R² = .22), and
/// neuroticism high (Germeijs &amp; Verschueren 2011, strongest correlate of indecisiveness, with
/// their own specificity finding as the caveat). The other eleven say plainly that nothing is
/// established and then offer a reading, or a population fact, instead.</para>
///
/// <para><b>Two things are forbidden outright</b> and no passage may reintroduce them under any
/// marking: anything on the review's "must never say" list (decision quality, susceptibility to
/// framing/anchoring/sunk cost, decision speed, maximising and regret, advice-taking differences,
/// or any suggestion that the profile lets the app advise better), and the agreeableness
/// advice-taking claim in particular. That last one is not merely unevidenced — it is contradicted:
/// Bailey et al. 2022 (N = 17,296) found <i>no</i> personality moderators of advice-taking, so the
/// old "you weigh how each option lands on the people around you" passage cannot be reused even
/// marked as interpretation. The agreeableness cells carry the population fact instead.</para>
///
/// Deterministic and MAUI-free by design, same as <see cref="ProfileNoteComposer"/>, so it can be
/// linked into <c>CoreChoice.App.Tests</c> and unit-tested directly.
/// </summary>
public static class ProfileTraitSummaryComposer
{
    private static readonly Trait[] AllTraits =
    [
        Trait.Openness, Trait.Conscientiousness, Trait.Extraversion, Trait.Agreeableness, Trait.Neuroticism,
    ];

    public static string Compose(Trait trait, TraitScore score) => Passages[trait][LevelOf(score)].Text;

    public static PassageTier TierOf(Trait trait, TraitScore score) => Passages[trait][LevelOf(score)].Tier;

    /// <summary>
    /// The five passages for one profile, in trait order, each tagged with its tier so the screen
    /// can group them rather than badge them.
    /// </summary>
    public static IReadOnlyList<TraitPassage> ComposeAll(OceanProfile profile) =>
        AllTraits.Select(trait =>
        {
            var level = LevelOf(profile[trait]);
            var passage = Passages[trait][level];
            return new TraitPassage(trait, trait.ToString(), level, passage.Tier, passage.Text);
        }).ToArray();

    private static TraitLevel LevelOf(TraitScore score) => score.DisplayBand() switch
    {
        "Very low" or "Low" => TraitLevel.Low,
        "Moderate" => TraitLevel.Moderate,
        _ => TraitLevel.High, // "High" or "Very high"
    };

    private sealed record Passage(PassageTier Tier, string Text);

    private static Passage Established(string text) => new(PassageTier.Established, text);

    private static Passage Interpreted(string text) => new(PassageTier.Interpretation, text);

    // Fifteen hand-written passages: five traits x three directions. Deliberately not twenty-five —
    // the display band already carries the intensity ("Very high" vs "High"); the passage carries
    // the meaning, and that meaning is the same for the inner and outer step on either side.
    //
    // Four are Established. Eleven are Interpretation, and each of those eleven says so in its own
    // first sentence as well as being grouped under the screen's second heading, so the distinction
    // survives a screenshot, a copy-paste, and a reader who never reaches the heading.
    private static readonly Dictionary<Trait, Dictionary<TraitLevel, Passage>> Passages = new()
    {
        [Trait.Openness] = new()
        {
            // Highhouse et al. 2022: ρ = .30, N = 69,125, Big Five R² = .22. Review §11 permits the
            // minimal mirror at the low end and nothing further.
            [TraitLevel.Low] = Established(
                "Scoring low here goes with a somewhat smaller appetite for risk. That is one " +
                "finding read from its other end, at the same modest strength, and nothing further " +
                "about how you decide has been established at this end of the scale."),
            [TraitLevel.Moderate] = Interpreted(
                "A middle score is the test declining to place you toward either the new or the " +
                "familiar, and nothing here is established. Every finding on this scale is measured " +
                "from one end to the other, so the middle of it has never been studied in its own " +
                "right."),
            [TraitLevel.High] = Established(
                "Scoring high here goes with a somewhat greater appetite for risk. It is the " +
                "clearest link between any of these five traits and how much risk a person will " +
                "take, drawn from 69,125 people, and it is still a modest one: a correlation of " +
                "0.30, with all five traits together accounting for 22% of what separates one " +
                "person's appetite for risk from another's. It is a tendency to check against " +
                "yourself rather than a description of you."),
        },
        [Trait.Conscientiousness] = new()
        {
            // Steel 2007, 691 correlations, r ≈ .63 — the only finding in the review strong enough
            // for plain second-person phrasing. Phillips et al. 2016 (r = .11) is the corrective
            // that has to travel with it, and §11 forbids sunk cost and impulsiveness outright.
            [TraitLevel.Low] = Established(
                "Scoring low here goes with putting decisions off, the same finding read from its " +
                "other end and one of the largest in the field. Both sides of it are self-report, " +
                "so some of what it measures is two ways of asking the same question rather than a " +
                "cause and an effect. It says nothing about whether the decisions you do make are " +
                "good ones, and the familiar claims about sunk cost and impulse have no evidence " +
                "behind them at all."),
            [TraitLevel.Moderate] = Interpreted(
                "The strong finding on this scale is a correlation measured end to end, which " +
                "leaves a middle score outside it and nothing established to say. A score here is " +
                "also the easiest kind to move on a retake, so it reads better as the test not " +
                "placing you than as a way of deciding."),
            [TraitLevel.High] = Established(
                "Scoring high here goes with not putting decisions off, and across 691 " +
                "correlations that is the largest and steadiest link in this whole research base. " +
                "It does not mean you decide better: how deliberately a person thinks predicts the " +
                "quality of their decisions only faintly, at a correlation of 0.11. What it predicts " +
                "is that the decision gets made."),
        },
        [Trait.Extraversion] = new()
        {
            // Review §11: recommend saying nothing at every level. Decision speed and overconfidence
            // are on the "must never say" list, so they appear here only as denials.
            [TraitLevel.Low] = Interpreted(
                "Nothing has been established at this end of the scale. One way to read it, with " +
                "no evidence behind it: the arguing may happen before anyone else hears about the " +
                "choice, so what reaches other people is a decision rather than a question."),
            [TraitLevel.Moderate] = Interpreted(
                "Nothing is established at either end of this scale, and a middle score has even " +
                "less behind it than the ends do. Neither the research nor this test has anything " +
                "to say about how you talk a decision over."),
            [TraitLevel.High] = Interpreted(
                "No decision pattern has been established at this end of the scale. Quicker " +
                "decisions, more confident ones, a more sociable way of choosing: each has been " +
                "looked for and none of it holds up. One way to read the score anyway, with " +
                "nothing behind it: a choice may not feel quite real until it has been said out " +
                "loud to someone."),
        },
        [Trait.Agreeableness] = new()
        {
            // Bailey et al. 2022, N = 17,296: no personality moderators of advice-taking at all.
            // The old passage here ("you weigh how each option lands on the people around you")
            // is contradicted rather than unevidenced and must not return in any marked form. What
            // stands in its place is the review's suggested population fact, which is about people
            // in general and says nothing about the reader.
            [TraitLevel.Low] = Interpreted(
                "Nothing is established at this end either, and the mirror claim — that a low " +
                "score means you discount what other people want — fails for the same reason the " +
                "high one does. How much of someone else's advice a person takes, measured across " +
                "17,296 people, tracks what they think of the adviser and not the traits they " +
                "carry."),
            [TraitLevel.Moderate] = Interpreted(
                "A middle score here sits in the least reliable part of the scale, on the trait " +
                "with the least to say about deciding. There is nothing established to report and " +
                "nothing worth inventing."),
            [TraitLevel.High] = Interpreted(
                "The obvious thing to say here — that you take other people's advice more " +
                "readily — is the one claim the research rules out. The largest study of " +
                "advice-taking, covering 17,296 people, found no trait that moved it at all. " +
                "People shift about 39% of the way toward advice they are given, and what changes " +
                "that number is how good they judge the adviser to be."),
        },
        [Trait.Neuroticism] = new()
        {
            // Germeijs & Verschueren 2011: strongest personality correlate of indecisiveness, with
            // the specificity finding as the caveat that must survive into copy. Steel 2007's
            // "neuroticism barely predicts procrastination" is the second corrective. §11 permits
            // nothing at the low end beyond the mirror.
            [TraitLevel.Low] = Interpreted(
                "The indecisiveness finding is measured at the other end of this scale, and " +
                "nothing has been established at this one. One way to read it, with no evidence " +
                "behind it: a decision you have made may simply stop asking for your attention, " +
                "which is quieter than the alternative without being any more correct."),
            [TraitLevel.Moderate] = Interpreted(
                "The one finding on this scale runs end to end and says nothing in particular " +
                "about the middle of it. A score here can move either way on a retake, which makes " +
                "it thin ground for reading anything."),
            [TraitLevel.High] = Established(
                "Scoring high here is the strongest link personality has to finding decisions hard " +
                "to settle. The study that found it also found its own limit: how indecisive you " +
                "feel predicts that difficulty better than this score does. It is not a predictor " +
                "of putting things off, despite the intuition that it would be."),
        },
    };
}
