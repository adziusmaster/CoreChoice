using System.Text.RegularExpressions;
using CoreChoice.Services;
using FluentAssertions;

namespace CoreChoice.App.Tests.Services;

public class VerdictLabelTests
{
    [Theory]
    [InlineData(100, "Clear direction")]
    [InlineData(80, "Clear direction")]
    [InlineData(79, "Strong case")]
    [InlineData(65, "Strong case")]
    [InlineData(64, "Slight edge")]
    [InlineData(50, "Slight edge")]
    [InlineData(49, "Genuinely close")]
    [InlineData(0, "Genuinely close")]
    public void For_AtEachBandBoundary_ShouldReturnTheExactLabel(int confidence, string expected)
    {
        // Arrange & Act
        var label = VerdictLabel.For(confidence);

        // Assert — the wording is approved product copy; it must match verbatim.
        label.Should().Be(expected);
    }

    [Fact]
    public void For_AcrossTheFullValidRange_ShouldNeverContainADigitOrPercentSign()
    {
        // Arrange — drive this from every input the function can see (0..100), not from the
        // four strings copied out of the implementation, so a future edit that reintroduces a
        // number is caught even if nobody remembers to update a hand-picked list.
        var confidences = Enumerable.Range(0, 101);

        // Act & Assert
        foreach (var confidence in confidences)
        {
            var label = VerdictLabel.For(confidence);

            label.Should().NotContain("%", $"label for confidence {confidence} must not show a percent sign");
            Regex.IsMatch(label, @"\d").Should().BeFalse(
                $"label '{label}' for confidence {confidence} must not contain a digit");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void For_WhenConfidenceIsBelowZero_ShouldClampToTheLowestBand(int confidence)
    {
        // Arrange & Act — the backend should never send this, but a malformed response must
        // degrade to the nearest valid band rather than throw or return nonsense.
        var label = VerdictLabel.For(confidence);

        // Assert
        label.Should().Be("Genuinely close");
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(int.MaxValue)]
    public void For_WhenConfidenceIsAboveOneHundred_ShouldClampToTheHighestBand(int confidence)
    {
        // Arrange & Act
        var label = VerdictLabel.For(confidence);

        // Assert
        label.Should().Be("Clear direction");
    }

    [Theory]
    [InlineData(-5, false)]
    [InlineData(0, false)]
    [InlineData(49, false)]
    [InlineData(50, false)]
    [InlineData(64, false)]
    [InlineData(65, true)]
    [InlineData(79, true)]
    [InlineData(80, true)]
    [InlineData(100, true)]
    [InlineData(200, true)]
    public void IsStrong_AroundTheStrongCaseBoundary_ShouldMatchTheTopTwoBands(int confidence, bool expected)
    {
        // Arrange & Act — 65 is the same line that separates "Strong case"/"Clear direction"
        // from "Slight edge"/"Genuinely close"; IsStrong reuses it rather than inventing a fifth.
        var isStrong = VerdictLabel.IsStrong(confidence);

        // Assert
        isStrong.Should().Be(expected);
    }
}
