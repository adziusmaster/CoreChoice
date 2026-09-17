using System.Collections;
using System.Reflection;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class ProfileTraitSummaryComposerTests
{
    private static readonly Trait[] AllTraits = Enum.GetValues<Trait>();
    private static readonly TraitLevel[] AllLevels = Enum.GetValues<TraitLevel>();

    private static readonly string[] TraitNames =
        AllTraits.Select(t => t.ToString().ToLowerInvariant()).ToArray();

    /// <summary>A score that lands squarely in the given direction, for driving the public API.</summary>
    private static TraitScore ScoreFor(TraitLevel level) => TraitScore.From(level switch
    {
        TraitLevel.Low => 10,
        TraitLevel.Moderate => 50,
        _ => 90,
    });

    /// <summary>Reads the composer's private passage table by reflection, exactly like
    /// <c>ProfileNoteComposerTests.GetPhraseTable</c> — there is no production reason to expose it
    /// publicly, only a test reason to prove it has no holes. The value type is a private nested
    /// record, so the two fields are pulled back out by name rather than cast.</summary>
    private static Dictionary<Trait, Dictionary<TraitLevel, (PassageTier Tier, string Text)>> GetPassages()
    {
        var field = typeof(ProfileTraitSummaryComposer).GetField("Passages", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "ProfileTraitSummaryComposer no longer has a private static field named 'Passages'. " +
                "Update this test to match, or restore the field.");

        var outer = (IDictionary)field.GetValue(null)!;
        var table = new Dictionary<Trait, Dictionary<TraitLevel, (PassageTier, string)>>();

        foreach (DictionaryEntry traitEntry in outer)
        {
            var inner = (IDictionary)traitEntry.Value!;
            var cells = new Dictionary<TraitLevel, (PassageTier, string)>();

            foreach (DictionaryEntry levelEntry in inner)
            {
                var passage = levelEntry.Value!;
                var type = passage.GetType();
                var tier = (PassageTier)type.GetProperty("Tier")!.GetValue(passage)!;
                var text = (string)type.GetProperty("Text")!.GetValue(passage)!;
                cells[(TraitLevel)levelEntry.Key!] = (tier, text);
            }

            table[(Trait)traitEntry.Key!] = cells;
        }

        return table;
    }

    // ---- Band mapping: "Very low" -> Low, "Very high" -> High -----------------------------

    [Theory]
    [InlineData(0, TraitLevel.Low)]     // "Very low"
    [InlineData(15, TraitLevel.Low)]    // "Very low" boundary
    [InlineData(20, TraitLevel.Low)]    // "Low"
    [InlineData(35, TraitLevel.Low)]    // "Low" boundary
    [InlineData(50, TraitLevel.Moderate)]
    [InlineData(65, TraitLevel.Moderate)] // "Moderate" boundary
    [InlineData(70, TraitLevel.High)]     // "High"
    [InlineData(85, TraitLevel.High)]     // "High" boundary
    [InlineData(90, TraitLevel.High)]     // "Very high"
    [InlineData(100, TraitLevel.High)]    // "Very high"
    public void Compose_MapsTheFiveDisplayBandsDownToThreeDirections(int scoreValue, TraitLevel expectedLevel)
    {
        // Arrange
        var score = TraitScore.From(scoreValue);
        var expected = GetPassages()[Trait.Openness][expectedLevel].Text;

        // Act
        var passage = ProfileTraitSummaryComposer.Compose(Trait.Openness, score);

        // Assert
        passage.Should().Be(expected);
    }

    // ---- Exact-text expectations: every trait x every direction ---------------------------
    // A passage silently swapped into the wrong direction (e.g. the high-openness text served for
    // low openness) is well-formed and would still pass a pure invariant sweep. Pinning the exact
    // wording per cell is what catches that class of bug — the same lesson ProfileNoteComposer
    // already shipped once. It is doubly load-bearing now that four of these cells assert a
    // finding: a research-backed sentence served for the wrong score is a false claim, not a
    // cosmetic slip.

    public const string OpennessLow =
        "Scoring low here goes with a somewhat smaller appetite for risk. That is one " +
        "finding read from its other end, at the same modest strength, and nothing further " +
        "about how you decide has been established at this end of the scale.";

    public const string OpennessModerate =
        "A middle score is the test declining to place you toward either the new or the " +
        "familiar, and nothing here is established. Every finding on this scale is measured " +
        "from one end to the other, so the middle of it has never been studied in its own " +
        "right.";

    public const string OpennessHigh =
        "Scoring high here goes with a somewhat greater appetite for risk. It is the " +
        "clearest link between any of these five traits and how much risk a person will " +
        "take, drawn from 69,125 people, and it is still a modest one: a correlation of " +
        "0.30, with all five traits together accounting for 22% of what separates one " +
        "person's appetite for risk from another's. It is a tendency to check against " +
        "yourself rather than a description of you.";

    public const string ConscientiousnessLow =
        "Scoring low here goes with putting decisions off, the same finding read from its " +
        "other end and one of the largest in the field. Both sides of it are self-report, " +
        "so some of what it measures is two ways of asking the same question rather than a " +
        "cause and an effect. It says nothing about whether the decisions you do make are " +
        "good ones, and the familiar claims about sunk cost and impulse have no evidence " +
        "behind them at all.";

    public const string ConscientiousnessModerate =
        "The strong finding on this scale is a correlation measured end to end, which " +
        "leaves a middle score outside it and nothing established to say. A score here is " +
        "also the easiest kind to move on a retake, so it reads better as the test not " +
        "placing you than as a way of deciding.";

    public const string ConscientiousnessHigh =
        "Scoring high here goes with not putting decisions off, and across 691 " +
        "correlations that is the largest and steadiest link in this whole research base. " +
        "It does not mean you decide better: how deliberately a person thinks predicts the " +
        "quality of their decisions only faintly, at a correlation of 0.11. What it predicts " +
        "is that the decision gets made.";

    public const string ExtraversionLow =
        "Nothing has been established at this end of the scale. One way to read it, with " +
        "no evidence behind it: the arguing may happen before anyone else hears about the " +
        "choice, so what reaches other people is a decision rather than a question.";

    public const string ExtraversionModerate =
        "Nothing is established at either end of this scale, and a middle score has even " +
        "less behind it than the ends do. Neither the research nor this test has anything " +
        "to say about how you talk a decision over.";

    public const string ExtraversionHigh =
        "No decision pattern has been established at this end of the scale. Quicker " +
        "decisions, more confident ones, a more sociable way of choosing: each has been " +
        "looked for and none of it holds up. One way to read the score anyway, with " +
        "nothing behind it: a choice may not feel quite real until it has been said out " +
        "loud to someone.";

    public const string AgreeablenessLow =
        "Nothing is established at this end either, and the mirror claim — that a low " +
        "score means you discount what other people want — fails for the same reason the " +
        "high one does. How much of someone else's advice a person takes, measured across " +
        "17,296 people, tracks what they think of the adviser and not the traits they " +
        "carry.";

    public const string AgreeablenessModerate =
        "A middle score here sits in the least reliable part of the scale, on the trait " +
        "with the least to say about deciding. There is nothing established to report and " +
        "nothing worth inventing.";

    public const string AgreeablenessHigh =
        "The obvious thing to say here — that you take other people's advice more " +
        "readily — is the one claim the research rules out. The largest study of " +
        "advice-taking, covering 17,296 people, found no trait that moved it at all. " +
        "People shift about 39% of the way toward advice they are given, and what changes " +
        "that number is how good they judge the adviser to be.";

    public const string NeuroticismLow =
        "The indecisiveness finding is measured at the other end of this scale, and " +
        "nothing has been established at this one. One way to read it, with no evidence " +
        "behind it: a decision you have made may simply stop asking for your attention, " +
        "which is quieter than the alternative without being any more correct.";

    public const string NeuroticismModerate =
        "The one finding on this scale runs end to end and says nothing in particular " +
        "about the middle of it. A score here can move either way on a retake, which makes " +
        "it thin ground for reading anything.";

    public const string NeuroticismHigh =
        "Scoring high here is the strongest link personality has to finding decisions hard " +
        "to settle. The study that found it also found its own limit: how indecisive you " +
        "feel predicts that difficulty better than this score does. It is not a predictor " +
        "of putting things off, despite the intuition that it would be.";

    public static IEnumerable<object[]> ExactPassages() =>
    [
        [Trait.Openness, TraitLevel.Low, 10, OpennessLow],
        [Trait.Openness, TraitLevel.Moderate, 50, OpennessModerate],
        [Trait.Openness, TraitLevel.High, 90, OpennessHigh],
        [Trait.Conscientiousness, TraitLevel.Low, 10, ConscientiousnessLow],
        [Trait.Conscientiousness, TraitLevel.Moderate, 50, ConscientiousnessModerate],
        [Trait.Conscientiousness, TraitLevel.High, 90, ConscientiousnessHigh],
        [Trait.Extraversion, TraitLevel.Low, 10, ExtraversionLow],
        [Trait.Extraversion, TraitLevel.Moderate, 50, ExtraversionModerate],
        [Trait.Extraversion, TraitLevel.High, 90, ExtraversionHigh],
        [Trait.Agreeableness, TraitLevel.Low, 10, AgreeablenessLow],
        [Trait.Agreeableness, TraitLevel.Moderate, 50, AgreeablenessModerate],
        [Trait.Agreeableness, TraitLevel.High, 90, AgreeablenessHigh],
        [Trait.Neuroticism, TraitLevel.Low, 10, NeuroticismLow],
        [Trait.Neuroticism, TraitLevel.Moderate, 50, NeuroticismModerate],
        [Trait.Neuroticism, TraitLevel.High, 90, NeuroticismHigh],
    ];

    [Theory]
    [MemberData(nameof(ExactPassages))]
    public void Compose_ForEveryTraitAndDirection_ShouldReturnItsExactHandWrittenPassage(
        Trait trait, TraitLevel level, int scoreValue, string expected)
    {
        // Arrange
        var score = TraitScore.From(scoreValue);

        // Act
        var passage = ProfileTraitSummaryComposer.Compose(trait, score);

        // Assert — pins the exact wording for this cell, not just "some non-empty string", so a
        // passage swapped into the wrong direction (e.g. High text served for Low) fails.
        passage.Should().Be(expected, $"{trait} at {level} must read its own hand-written passage");

        // And it must not silently be a different direction's passage for the same trait.
        foreach (var otherLevel in AllLevels.Where(l => l != level))
        {
            passage.Should().NotBe(
                GetPassages()[trait][otherLevel].Text,
                $"{trait} at {level} must not read the same as {trait} at {otherLevel}");
        }
    }

    // ---- Tier: which cells are allowed to claim research backing --------------------------
    // docs/research/big-five-decision-making.md §11 permits exactly four speaking slots out of
    // fifteen. This is the test that keeps the distinction real rather than cosmetic: a passage
    // rewritten into a confident claim, or promoted into the screen's "what research supports"
    // group, fails here by name.

    public static IEnumerable<object[]> ExpectedTiers() =>
    [
        // The four findings the review permits to speak — conscientiousness (Steel 2007),
        // openness (Highhouse et al. 2022) and neuroticism high (Germeijs & Verschueren 2011) —
        // across the five cells that carry them. Openness low is the fifth cell and not a fifth
        // finding: it is the openness risk correlation mirrored at its minimal reading, which the
        // review's own table marks "speak, minimal". It sits in the established tier because its
        // text states that finding, and a passage that states a finding must not be filed under
        // interpretation.
        [Trait.Conscientiousness, TraitLevel.High, PassageTier.Established],
        [Trait.Conscientiousness, TraitLevel.Low, PassageTier.Established],
        [Trait.Openness, TraitLevel.High, PassageTier.Established],
        [Trait.Openness, TraitLevel.Low, PassageTier.Established],
        [Trait.Neuroticism, TraitLevel.High, PassageTier.Established],
        // Every other cell: nothing established.
        [Trait.Openness, TraitLevel.Moderate, PassageTier.Interpretation],
        [Trait.Conscientiousness, TraitLevel.Moderate, PassageTier.Interpretation],
        [Trait.Extraversion, TraitLevel.Low, PassageTier.Interpretation],
        [Trait.Extraversion, TraitLevel.Moderate, PassageTier.Interpretation],
        [Trait.Extraversion, TraitLevel.High, PassageTier.Interpretation],
        [Trait.Agreeableness, TraitLevel.Low, PassageTier.Interpretation],
        [Trait.Agreeableness, TraitLevel.Moderate, PassageTier.Interpretation],
        [Trait.Agreeableness, TraitLevel.High, PassageTier.Interpretation],
        [Trait.Neuroticism, TraitLevel.Low, PassageTier.Interpretation],
        [Trait.Neuroticism, TraitLevel.Moderate, PassageTier.Interpretation],
    ];

    [Theory]
    [MemberData(nameof(ExpectedTiers))]
    public void TierOf_ForEveryTraitAndDirection_ShouldMatchWhatTheReviewPermits(
        Trait trait, TraitLevel level, PassageTier expected)
    {
        // Arrange
        var score = ScoreFor(level);

        // Act
        var tier = ProfileTraitSummaryComposer.TierOf(trait, score);

        // Assert
        tier.Should().Be(expected,
            $"{trait} at {level} must be {expected} — docs/research/big-five-decision-making.md §11 " +
            "permits research-backed phrasing for conscientiousness (high and low), openness (high " +
            "and its minimal mirror at low), and neuroticism (high), and for nothing else");
    }

    [Fact]
    public void Passages_ShouldClaimResearchBackingForExactlyTheFivePermittedCells()
    {
        // Arrange
        var table = GetPassages();

        // Act
        var established = table
            .SelectMany(trait => trait.Value
                .Where(cell => cell.Value.Tier == PassageTier.Established)
                .Select(cell => $"{trait.Key}/{cell.Key}"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Assert — a whole-table count, so a sixth cell quietly promoted to Established fails even
        // if someone forgets to add it to ExpectedTiers above.
        established.Should().BeEquivalentTo(
        [
            "Conscientiousness/High",
            "Conscientiousness/Low",
            "Neuroticism/High",
            "Openness/High",
            "Openness/Low",
        ], "only the cells the literature review permits may claim research backing");
    }

    // ---- The contradicted claim: agreeableness and advice-taking --------------------------

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(90)]
    public void Compose_ForAgreeableness_ShouldNotReviveTheContradictedAdviceTakingClaim(int scoreValue)
    {
        // Arrange — Bailey et al. 2022 (N = 17,296) found no personality moderators of
        // advice-taking at all, so this claim is contradicted rather than merely unevidenced and
        // may not return in any form, marked or otherwise. These fragments are the retired passage.
        var score = TraitScore.From(scoreValue);
        string[] retired =
        [
            "lands on the people around you",
            "sometimes more than to what you actually want",
            "easy to decide with",
            "keeps the room comfortable",
            "automatically deferring",
        ];

        // Act
        var passage = ProfileTraitSummaryComposer.Compose(Trait.Agreeableness, score);

        // Assert
        foreach (var fragment in retired)
        {
            passage.Should().NotContain(fragment,
                $"the retired advice-taking passage is contradicted by the evidence and must not " +
                $"come back at score {scoreValue}: \"{passage}\"");
        }
    }

    // ---- Sweep: well-formedness across every trait x every direction ----------------------

    public static IEnumerable<object[]> AllCells() =>
        AllTraits.SelectMany(t => AllLevels.Select(l => new object[] { t, l }));

    [Theory]
    [MemberData(nameof(AllCells))]
    public void Compose_AcrossEveryTraitAndDirection_ShouldAlwaysProduceAWellFormedPassage(Trait trait, TraitLevel level)
    {
        // Arrange
        var score = ScoreFor(level);
        var description = $"{trait} at {level}";

        // Act
        var passage = ProfileTraitSummaryComposer.Compose(trait, score);

        // Assert — every invariant a well-formed passage must satisfy, named to the cell that
        // produced it so a failure points straight at the broken entry.
        passage.Should().NotBeNullOrWhiteSpace($"the passage for [{description}] must not be blank");

        passage.Should().NotContain(", .", $"the passage for [{description}] has an empty clause: \"{passage}\"");
        passage.Should().NotContain(" .", $"the passage for [{description}] has a dangling space before its full stop: \"{passage}\"");
        passage.Should().NotContain(",,", $"the passage for [{description}] has a doubled comma: \"{passage}\"");
        passage.Should().NotContain("  ", $"the passage for [{description}] has a doubled space: \"{passage}\"");
        passage.Should().NotContain("!", $"the passage for [{description}] must never use an exclamation mark: \"{passage}\"");

        char.IsUpper(passage[0]).Should().BeTrue($"the passage for [{description}] must start with a capital letter: \"{passage}\"");
        passage.Should().EndWith(".", $"the passage for [{description}] must end with a full stop: \"{passage}\"");

        var lowered = passage.ToLowerInvariant();
        foreach (var traitName in TraitNames)
        {
            lowered.Should().NotContain(traitName,
                $"the passage for [{description}] leaked the trait name \"{traitName}\" instead of describing it: \"{passage}\"");
        }

        lowered.Should().NotContain("you should",
            $"the passage for [{description}] gives advice instead of describing: \"{passage}\"");
        lowered.Should().NotContain("scientific",
            $"the passage for [{description}] uses \"scientific\" as decoration: \"{passage}\"");

        passage.Should().NotContain("{", $"the passage for [{description}] left placeholder residue: \"{passage}\"");
        passage.Should().NotContain("}", $"the passage for [{description}] left placeholder residue: \"{passage}\"");
    }

    [Theory]
    [MemberData(nameof(AllCells))]
    public void Compose_ForEveryInterpretationCell_ShouldSayThatNothingIsEstablished(Trait trait, TraitLevel level)
    {
        // Arrange — grouping under a heading is the primary signal, but a passage read alone (a
        // screenshot, a paste into a message) has to carry the marking too. Every interpretive
        // passage says so in its own words; no established one needs to.
        var score = ScoreFor(level);
        var tier = ProfileTraitSummaryComposer.TierOf(trait, score);

        // Act
        var passage = ProfileTraitSummaryComposer.Compose(trait, score).ToLowerInvariant();

        // Assert
        if (tier == PassageTier.Interpretation)
        {
            var marked = passage.Contains("established")
                || passage.Contains("no evidence")
                || passage.Contains("rules out")
                || passage.Contains("nothing in particular");

            marked.Should().BeTrue(
                $"the interpretive passage for [{trait} at {level}] must say in its own words that " +
                $"nothing is established: \"{passage}\"");
        }
    }

    // ---- Completeness: every cell must exist and be non-blank, named by cell --------------

    [Fact]
    public void Passages_ForEveryTraitAndDirection_ShouldHaveANonEmptyEntry()
    {
        // Arrange
        var table = GetPassages();

        // Act & Assert — check every trait/direction cell individually so a missing or blank one
        // is named by exactly which cell is broken, not just "some cell somewhere is missing".
        foreach (var trait in AllTraits)
        {
            table.Should().ContainKey(trait, $"Passages[{trait}] must exist");

            foreach (var level in AllLevels)
            {
                table[trait].Should().ContainKey(level, $"Passages[{trait}][{level}] must exist");
                table[trait][level].Text.Should().NotBeNullOrWhiteSpace($"Passages[{trait}][{level}] must not be blank");
            }
        }
    }

    [Fact]
    public void Passages_ShouldBeFifteenDistinctTexts()
    {
        // Arrange
        var table = GetPassages();

        // Act
        var texts = table.SelectMany(t => t.Value.Values.Select(v => v.Text)).ToArray();

        // Assert — fifteen cells, no cell reused for another, so a copy-paste that leaves two
        // traits sharing one passage fails here rather than shipping.
        texts.Should().HaveCount(15);
        texts.Should().OnlyHaveUniqueItems("no two cells may share the same passage");
    }

    // ---- ComposeAll: the shape the screen binds to ----------------------------------------

    [Fact]
    public void ComposeAll_ShouldReturnOnePassagePerTraitInTraitOrderWithItsTier()
    {
        // Arrange — high openness, low conscientiousness, moderate extraversion, high
        // agreeableness, low neuroticism.
        var profile = new OceanProfile(
            TraitScore.From(90), TraitScore.From(10), TraitScore.From(50),
            TraitScore.From(90), TraitScore.From(10));

        // Act
        var passages = ProfileTraitSummaryComposer.ComposeAll(profile);

        // Assert
        passages.Select(p => p.Trait).Should().Equal(
            Trait.Openness, Trait.Conscientiousness, Trait.Extraversion, Trait.Agreeableness, Trait.Neuroticism);
        passages.Select(p => p.TraitName).Should().Equal(
            "Openness", "Conscientiousness", "Extraversion", "Agreeableness", "Neuroticism");
        passages.Select(p => p.Level).Should().Equal(
            TraitLevel.High, TraitLevel.Low, TraitLevel.Moderate, TraitLevel.High, TraitLevel.Low);
        passages.Select(p => p.Tier).Should().Equal(
            PassageTier.Established, PassageTier.Established, PassageTier.Interpretation,
            PassageTier.Interpretation, PassageTier.Interpretation);
        passages.Select(p => p.Text).Should().Equal(
            OpennessHigh, ConscientiousnessLow, ExtraversionModerate, AgreeablenessHigh, NeuroticismLow);
    }

    [Fact]
    public void ComposeAll_ForAnEntirelyModerateProfile_ShouldClaimNoResearchBackingAtAll()
    {
        // Arrange — the profile the review is bluntest about: nothing studies mid-scorers.
        var profile = new OceanProfile(
            TraitScore.From(50), TraitScore.From(50), TraitScore.From(50),
            TraitScore.From(50), TraitScore.From(50));

        // Act
        var passages = ProfileTraitSummaryComposer.ComposeAll(profile);

        // Assert
        passages.Should().OnlyContain(p => p.Tier == PassageTier.Interpretation,
            "no moderate score may be presented as research-backed");
    }
}
