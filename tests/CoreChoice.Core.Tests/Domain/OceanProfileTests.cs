using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.Core.Tests.Domain;

public class OceanProfileTests
{
    [Fact]
    public void From_WhenValueIsOutOfRange_ShouldThrow()
    {
        // Arrange
        const int tooHigh = 101;

        // Act
        var act = () => TraitScore.From(tooHigh);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, "low")]
    [InlineData(32, "low")]
    [InlineData(33, "moderate")]
    [InlineData(66, "moderate")]
    [InlineData(67, "high")]
    [InlineData(100, "high")]
    public void Band_ForScore_ShouldMapToExpectedBand(int value, string expected)
    {
        // Arrange
        var score = TraitScore.From(value);

        // Act
        var band = score.Band;

        // Assert
        band.Should().Be(expected);
    }

    [Fact]
    public void IsPresent_ForNone_ShouldBeFalse()
    {
        // Arrange
        var profile = OceanProfile.None;

        // Act
        var present = profile.IsPresent;

        // Assert
        present.Should().BeFalse();
    }

    [Fact]
    public void IsPresent_ForScoredProfile_ShouldBeTrue()
    {
        // Arrange
        var profile = new OceanProfile(
            TraitScore.From(70), TraitScore.From(60), TraitScore.From(50),
            TraitScore.From(80), TraitScore.From(30));

        // Act
        var present = profile.IsPresent;

        // Assert
        present.Should().BeTrue();
    }

    [Fact]
    public void Indexer_ForEachTrait_ShouldReturnThatTraitsScore()
    {
        // Arrange
        var profile = new OceanProfile(
            TraitScore.From(10), TraitScore.From(20), TraitScore.From(30),
            TraitScore.From(40), TraitScore.From(50));

        // Act
        var agreeableness = profile[Trait.Agreeableness];

        // Assert
        agreeableness.Value.Should().Be(40);
    }

    [Fact]
    public void None_ShouldBeDistinguishableFromAGenuineAllZeroProfile()
    {
        // Arrange — "has not taken the test" and "scored zero on everything" are different facts.
        // If IsPresent were ever derived from the trait values instead of set at construction,
        // these two would collapse into one and every unprofiled analysis would claim to be
        // personalized. This test is the tripwire for that refactor.
        var genuineAllZero = new OceanProfile(
            TraitScore.From(0), TraitScore.From(0), TraitScore.From(0),
            TraitScore.From(0), TraitScore.From(0));

        // Act
        var none = OceanProfile.None;

        // Assert
        none.IsPresent.Should().BeFalse();
        genuineAllZero.IsPresent.Should().BeTrue();
        none.Should().NotBe(genuineAllZero);
    }
}
