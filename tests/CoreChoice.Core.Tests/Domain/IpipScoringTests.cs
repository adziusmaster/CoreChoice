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

    [Fact]
    public void Score_WhenEveryAnswerIsMaximum_ShouldReflectReverseKeying()
    {
        // Arrange — answering 5 to everything means "very accurate" to both
        // "Am the life of the party" and "Keep in the background", so every trait
        // lands mid-scale. A naive implementation that ignores reverse keys returns 100.
        var responses = AllAnswered(5);

        // Act
        var profile = IpipScoring.Score(responses);

        // Assert
        profile.IsPresent.Should().BeTrue();
        foreach (var trait in Enum.GetValues<Trait>())
            profile[trait].Value.Should().Be(50, $"{trait} should be mid-scale when all answers agree");
    }

    [Fact]
    public void Score_WhenForwardItemsMaxAndReverseItemsMin_ShouldReturnHundred()
    {
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
}
