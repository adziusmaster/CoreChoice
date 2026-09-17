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

    /// <summary>Reads the composer's private passage table by reflection, exactly like
    /// <c>ProfileNoteComposerTests.GetPhraseTable</c> — there is no production reason to expose it
    /// publicly, only a test reason to prove it has no holes.</summary>
    private static Dictionary<Trait, Dictionary<TraitLevel, string>> GetPassages()
    {
        var field = typeof(ProfileTraitSummaryComposer).GetField("Passages", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "ProfileTraitSummaryComposer no longer has a private static field named 'Passages'. " +
                "Update this test to match, or restore the field.");

        return (Dictionary<Trait, Dictionary<TraitLevel, string>>)field.GetValue(null)!;
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
        var expected = GetPassages()[Trait.Openness][expectedLevel];

        // Act
        var passage = ProfileTraitSummaryComposer.Compose(Trait.Openness, score);

        // Assert
        passage.Should().Be(expected);
    }

    // ---- Exact-text expectations: every trait x every direction ---------------------------
    // A passage silently swapped into the wrong direction (e.g. the high-openness text served for
    // low openness) is well-formed and would still pass a pure invariant sweep. Pinning the exact
    // wording per cell is what catches that class of bug — the same lesson ProfileNoteComposer
    // already shipped once.

    public static IEnumerable<object[]> ExactPassages()
    {
        yield return new object[]
        {
            Trait.Openness, TraitLevel.Low, 10,
            "Between two options, you lean toward the one with a track record, and a new " +
            "alternative has to earn your attention before you spend it there. That keeps you " +
            "from chasing whatever looks shiny, but it can also mean a genuinely better, " +
            "unfamiliar option never quite registers as a real choice.",
        };
        yield return new object[]
        {
            Trait.Openness, TraitLevel.Moderate, 50,
            "You weigh an unfamiliar option and a proven one on their own merits rather than " +
            "favoring either out of habit, which keeps you open without being restless. The " +
            "trade-off is a slower decision than either a committed traditionalist or a " +
            "committed experimenter would make, since neither side wins by default.",
        };
        yield return new object[]
        {
            Trait.Openness, TraitLevel.High, 90,
            "Given two options, the one nobody has tried yet pulls at you before you have " +
            "finished checking whether it actually solves the problem. That keeps your choices " +
            "wide open, but it also means the sufficient, familiar option can look dull by " +
            "comparison even when it is the better answer.",
        };
        yield return new object[]
        {
            Trait.Conscientiousness, TraitLevel.Low, 10,
            "You decide as you go rather than laying the choice out in advance, which keeps you " +
            "moving on decisions that do not need much ceremony. The cost shows up later, when a " +
            "commitment made casually turns out to need follow-through you did not plan for.",
        };
        yield return new object[]
        {
            Trait.Conscientiousness, TraitLevel.Moderate, 50,
            "You plan the decisions that seem to warrant it and let the rest happen more loosely, " +
            "judging case by case instead of following one fixed process. That keeps you from " +
            "over-engineering small choices, though the line between what deserves planning and " +
            "what does not moves depending on the day.",
        };
        yield return new object[]
        {
            Trait.Conscientiousness, TraitLevel.High, 90,
            "Before committing, you want the criteria settled and the steps after the decision " +
            "already mapped, which is why what you choose tends to actually get carried through. " +
            "The same instinct can turn a genuinely open-ended choice into more organizing than " +
            "the decision itself required.",
        };
        yield return new object[]
        {
            Trait.Extraversion, TraitLevel.Low, 10,
            "You work a decision through on your own rather than think it out loud, and by the " +
            "time you commit you have already argued with yourself about it. That gives you a " +
            "choice tested against your own doubts, but it also means you can settle on a " +
            "direction before hearing something that would have changed it.",
        };
        yield return new object[]
        {
            Trait.Extraversion, TraitLevel.Moderate, 50,
            "You will talk a decision over with someone when that helps and sit with it alone " +
            "when it does not, without a strong pull toward either. That gives you both routes, " +
            "but neither is where you start by instinct, so deciding how to decide can take " +
            "almost as long as deciding.",
        };
        yield return new object[]
        {
            Trait.Extraversion, TraitLevel.High, 90,
            "You think best with a decision said out loud, tried on someone else before it feels " +
            "real. That gets you a fast read on how a choice will land, but a decision made in a " +
            "quiet room with no one to react to can feel harder to trust than it actually is.",
        };
        yield return new object[]
        {
            Trait.Agreeableness, TraitLevel.Low, 10,
            "When two options serve different people, you weigh what you actually want ahead of " +
            "what keeps the room comfortable. That means your choice holds up under your own " +
            "scrutiny even when it is unpopular, but it can also underweight a cost that lands on " +
            "someone else rather than on you.",
        };
        yield return new object[]
        {
            Trait.Agreeableness, TraitLevel.Moderate, 50,
            "You take other people's stake in a decision seriously without automatically " +
            "deferring to it, which keeps a choice from becoming only about keeping the peace. " +
            "What that produces is a compromise more often than a clean answer, sometimes at the " +
            "cost of the better, less comfortable option.",
        };
        yield return new object[]
        {
            Trait.Agreeableness, TraitLevel.High, 90,
            "Choosing between options, you give real weight to how each one lands on the people " +
            "around you, sometimes more than to what you actually want. That makes you easy to " +
            "decide with, but the option that costs you something personally can look reasonable " +
            "simply because it costs someone else less.",
        };
        yield return new object[]
        {
            Trait.Neuroticism, TraitLevel.Low, 10,
            "A decision that could go badly does not occupy much space in you once it is made; " +
            "you commit and move on rather than replaying it. That keeps second-guessing from " +
            "eating time a choice does not need, but a genuine warning sign can get the same " +
            "shrug as ordinary noise.",
        };
        yield return new object[]
        {
            Trait.Neuroticism, TraitLevel.Moderate, 50,
            "Some decisions sit with you afterward and some do not, roughly in proportion to what " +
            "was actually at stake. That is a fairly accurate alarm, though it still fires early " +
            "often enough that you will sometimes brace for a consequence that never arrives.",
        };
        yield return new object[]
        {
            Trait.Neuroticism, TraitLevel.High, 90,
            "Once a decision is made, you keep turning it over, alert to what could still go " +
            "wrong even with nothing left to change. That vigilance catches real risk other " +
            "people miss, but a decision that turns out fine can still leave a long tail of " +
            "worry behind it.",
        };
    }

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
                GetPassages()[trait][otherLevel],
                $"{trait} at {level} must not read the same as {trait} at {otherLevel}");
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
        var scoreValue = level switch
        {
            TraitLevel.Low => 10,
            TraitLevel.Moderate => 50,
            _ => 90,
        };
        var score = TraitScore.From(scoreValue);
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

        passage.Should().NotContain("{", $"the passage for [{description}] left placeholder residue: \"{passage}\"");
        passage.Should().NotContain("}", $"the passage for [{description}] left placeholder residue: \"{passage}\"");
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
                table[trait][level].Should().NotBeNullOrWhiteSpace($"Passages[{trait}][{level}] must not be blank");
            }
        }
    }
}
