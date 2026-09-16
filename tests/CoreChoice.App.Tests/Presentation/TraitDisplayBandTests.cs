using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class TraitDisplayBandTests
{
    [Theory]
    [InlineData(0, "Very low")]
    [InlineData(15, "Very low")]
    [InlineData(16, "Low")]
    [InlineData(35, "Low")]
    [InlineData(36, "Moderate")]
    [InlineData(50, "Moderate")]
    [InlineData(65, "Moderate")]
    [InlineData(66, "High")]
    [InlineData(85, "High")]
    [InlineData(86, "Very high")]
    [InlineData(100, "Very high")]
    public void DisplayBand_AtEveryBoundaryOnBothSides_ShouldReturnTheExpectedFiveLevelLabel(int value, string expected)
    {
        // Arrange
        var score = TraitScore.From(value);

        // Act
        var band = score.DisplayBand();

        // Assert
        band.Should().Be(expected);
    }

    [Fact]
    public void DisplayBand_ForScoresTheCoarseBandTreatsIdentically_ShouldStillTellThemApart()
    {
        // Arrange — 70 and 90 both read "high" on TraitScore.Band (the server-prompt band), which
        // is exactly the coarseness the result screen needs to get past.
        var high = TraitScore.From(70);
        var veryHigh = TraitScore.From(90);

        // Act
        var highBand = high.DisplayBand();
        var veryHighBand = veryHigh.DisplayBand();

        // Assert
        high.Band.Should().Be(veryHigh.Band);
        highBand.Should().Be("High");
        veryHighBand.Should().Be("Very high");
        highBand.Should().NotBe(veryHighBand);
    }
}
