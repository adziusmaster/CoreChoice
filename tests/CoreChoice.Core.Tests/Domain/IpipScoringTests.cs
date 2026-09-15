using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.Core.Tests.Domain;

public class IpipScoringTests
{
    private static Dictionary<int, int> AllAnswered(int value) =>
        IpipItemBank.Items.ToDictionary(i => i.Number, _ => value);

    [Fact]
    public void Items_ShouldContainFiftyItemsTenPerTrait()
    {
        // Arrange
        var items = IpipItemBank.Items;

        // Act
        var perTrait = items.GroupBy(i => i.Trait).ToDictionary(g => g.Key, g => g.Count());

        // Assert
        items.Should().HaveCount(50);
        items.Select(i => i.Number).Should().OnlyHaveUniqueItems();
        perTrait.Should().HaveCount(5);
        perTrait.Values.Should().AllBeEquivalentTo(10);
    }

    [Theory]
    [InlineData(Trait.Openness, new[] { 10, 20, 30 })]
    [InlineData(Trait.Conscientiousness, new[] { 8, 18, 28, 38 })]
    [InlineData(Trait.Extraversion, new[] { 6, 16, 26, 36, 46 })]
    [InlineData(Trait.Agreeableness, new[] { 2, 12, 22, 32 })]
    [InlineData(Trait.Neuroticism, new[] { 9, 19 })]
    public void Items_ReverseKeyedNumbers_ShouldMatchTheInstrument(Trait trait, int[] expected)
    {
        // Arrange — the keying is DATA, and data can be silently wrong in a way no scoring test
        // catches. This pins every flag by item number, so flipping a single one fails the build.
        // The counts are deliberately uneven (3/4/5/4/2): the instrument's items are simply not
        // written with balanced polarity, and "tidying" them to 5-per-trait inverts item meanings.
        var items = IpipItemBank.Items.Where(i => i.Trait == trait);

        // Act
        var reverseKeyed = items.Where(i => i.IsReverseKeyed).Select(i => i.Number).OrderBy(n => n);

        // Assert
        reverseKeyed.Should().Equal(expected);
    }

    [Fact]
    public void Score_WhenEveryAnswerIsMaximum_ShouldReflectEachTraitsReverseKeyCount()
    {
        // Arrange — answering 5 to everything means "very accurate" to both "Am the life of the
        // party" and "Keep in the background". Each trait therefore lands at 100 - 10r, where r is
        // its reverse-keyed count. The five expected values are DISTINCT, which is what makes this
        // test keying-sensitive: it pins each trait's reverse count individually.
        //
        // Do NOT "simplify" these to five identical 50s. That is only true of a balanced
        // instrument, and an earlier revision of this plan asserted exactly that — which made the
        // test unsatisfiable against the real bank and led an implementer to rewrite the
        // questionnaire to fit the test.
        var responses = AllAnswered(5);

        // Act
        var profile = IpipScoring.Score(responses);

        // Assert — a naive implementation ignoring reverse keys returns 100 for all five.
        profile.IsPresent.Should().BeTrue();
        profile[Trait.Openness].Value.Should().Be(70);
        profile[Trait.Conscientiousness].Value.Should().Be(60);
        profile[Trait.Extraversion].Value.Should().Be(50);
        profile[Trait.Agreeableness].Value.Should().Be(60);
        profile[Trait.Neuroticism].Value.Should().Be(80);
    }

    [Fact]
    public void Score_WhenForwardItemsMaxAndReverseItemsMin_ShouldReturnHundred()
    {
        // NOTE: this test and the two below derive their inputs FROM IsReverseKeyed, so every item
        // contributes the same value whatever its flag. They constrain the normalisation endpoints
        // and trait independence — they do NOT constrain the keying, and would pass on a bank with
        // all 50 flags set. Keying is pinned by Items_ReverseKeyedNumbers_ShouldMatchTheInstrument
        // and by the all-maximum test above. Do not treat these three as keying coverage.

        // Arrange
        var responses = IpipItemBank.Items.ToDictionary(
            i => i.Number,
            i => i.IsReverseKeyed ? 1 : 5);

        // Act
        var profile = IpipScoring.Score(responses);

        // Assert
        foreach (var trait in Enum.GetValues<Trait>())
            profile[trait].Value.Should().Be(100);
    }

    [Fact]
    public void Score_WhenForwardItemsMinAndReverseItemsMax_ShouldReturnZero()
    {
        // Arrange
        var responses = IpipItemBank.Items.ToDictionary(
            i => i.Number,
            i => i.IsReverseKeyed ? 5 : 1);

        // Act
        var profile = IpipScoring.Score(responses);

        // Assert
        foreach (var trait in Enum.GetValues<Trait>())
            profile[trait].Value.Should().Be(0);
    }

    [Fact]
    public void Score_ShouldScoreEachTraitIndependently()
    {
        // Arrange — max out extraversion only, hold everything else at the midpoint.
        var responses = IpipItemBank.Items.ToDictionary(
            i => i.Number,
            i => i.Trait == Trait.Extraversion
                ? (i.IsReverseKeyed ? 1 : 5)
                : 3);

        // Act
        var profile = IpipScoring.Score(responses);

        // Assert
        profile[Trait.Extraversion].Value.Should().Be(100);
        profile[Trait.Openness].Value.Should().Be(50);
        profile[Trait.Agreeableness].Value.Should().Be(50);
    }

    [Fact]
    public void Score_WhenAnswersAreMissing_ShouldThrowIncompleteProfile()
    {
        // Arrange
        var responses = AllAnswered(3);
        responses.Remove(17);

        // Act
        var act = () => IpipScoring.Score(responses);

        // Assert
        act.Should().Throw<IncompleteProfileException>()
            .Which.Answered.Should().Be(49);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Score_WhenAnAnswerIsOutsideTheLikertRange_ShouldThrow(int invalid)
    {
        // Arrange
        var responses = AllAnswered(3);
        responses[1] = invalid;

        // Act
        var act = () => IpipScoring.Score(responses);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Score_WhenAnAnswerIsAtTheLikertBoundary_ShouldNotThrow(int boundary)
    {
        // Arrange — 1 and 5 are valid Likert extremes, not out-of-range values.
        var responses = AllAnswered(3);
        responses[1] = boundary;

        // Act
        var act = () => IpipScoring.Score(responses);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Score_WhenGivenAnUnknownItemNumber_ShouldThrow()
    {
        // Arrange
        var responses = AllAnswered(3);
        responses[999] = 4;

        // Act
        var act = () => IpipScoring.Score(responses);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Score_WhenResponsesIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        IReadOnlyDictionary<int, int>? responses = null;

        // Act
        var act = () => IpipScoring.Score(responses!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
