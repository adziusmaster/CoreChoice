using CoreChoice.Presentation;
using CoreChoice.Services;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

/// <summary>
/// Covers the maths pulled out of <c>PresentationConverters.cs</c>. That file cannot be linked
/// into this project at all — it implements <c>IValueConverter</c> and uses <c>Rect</c>, both
/// <c>Microsoft.Maui.*</c> types, and this project targets plain <c>net10.0</c>. Before the split,
/// that meant all five converters — including the dot-position maths that places a mark on every
/// trait row on the profile screen — had zero automated coverage.
/// </summary>
public class PresentationMathTests
{
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(50, 0.5)]
    [InlineData(100, 1.0)]
    [InlineData(1, 0.01)]
    [InlineData(99, 0.99)]
    [InlineData(33, 0.33)] // an int-division bug (traitValue / 100) would collapse this to 0.
    public void TraitDotBounds_AcrossTheFullScoreRange_ShouldPlaceXAtTheExactFractionWithNoTruncation(
        int traitValue, double expectedX)
    {
        // Arrange & Act
        var bounds = PresentationMath.TraitDotBounds(traitValue);

        // Assert
        bounds.X.Should().BeApproximately(expectedX, 1e-9);
    }

    [Fact]
    public void TraitDotBounds_AtZero_ShouldNotClipTheDotInsideTheTrack()
    {
        // Arrange & Act
        var bounds = PresentationMath.TraitDotBounds(0);

        // Assert — X must reach the true start of the track, not a clamped-in value.
        bounds.X.Should().Be(0.0);
    }

    [Fact]
    public void TraitDotBounds_AtOneHundred_ShouldNotClipTheDotInsideTheTrack()
    {
        // Arrange & Act
        var bounds = PresentationMath.TraitDotBounds(100);

        // Assert — X must reach the true end of the track, not a clamped-in value.
        bounds.X.Should().Be(1.0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void TraitDotBounds_AtAnyScore_ShouldKeepTheDotSizeAndVerticalCentreFixed(int traitValue)
    {
        // Arrange & Act
        var bounds = PresentationMath.TraitDotBounds(traitValue);

        // Assert
        bounds.Y.Should().Be(0.5);
        bounds.Width.Should().Be(10);
        bounds.Height.Should().Be(10);
    }

    [Fact]
    public void IsLikertSelected_WhenResponseMatchesTheParameter_ShouldBeTrue()
    {
        // Arrange & Act
        var selected = PresentationMath.IsLikertSelected(3, "3");

        // Assert
        selected.Should().BeTrue();
    }

    [Theory]
    [InlineData(3, "4")]
    [InlineData(null, "3")]
    [InlineData(3, "not-a-number")]
    [InlineData(3, null)]
    public void IsLikertSelected_WhenResponseDoesNotMatchOrEitherInputIsUnusable_ShouldBeFalse(
        int? response, string? parameterText)
    {
        // Arrange & Act
        var selected = PresentationMath.IsLikertSelected(response, parameterText);

        // Assert
        selected.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 50, "1 of 50")]
    [InlineData(1, 50, "2 of 50")]
    [InlineData(49, 50, "50 of 50")]
    [InlineData(50, 50, "50 of 50")] // every item answered: must not overrun the total.
    public void FormatTestPosition_AcrossTheAnsweredRange_ShouldReportTheNextItemCappedAtTheTotal(
        int answeredCount, int itemCount, string expected)
    {
        // Arrange & Act
        var label = PresentationMath.FormatTestPosition(answeredCount, itemCount);

        // Assert
        label.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", true)]
    [InlineData("Two coins added.", true)]
    public void IsNotEmpty_ForVariousStrings_ShouldMatchStringIsNullOrEmpty(string? value, bool expected)
    {
        // Arrange & Act
        var isNotEmpty = PresentationMath.IsNotEmpty(value);

        // Assert
        isNotEmpty.Should().Be(expected);
    }

    [Theory]
    [InlineData(Palette.Considered, "Considered", true)]
    [InlineData(Palette.Composed, "Considered", false)]
    [InlineData(Palette.Still, "Still", true)]
    [InlineData(Palette.Considered, null, false)]
    [InlineData(Palette.Considered, "considered", false)] // case-sensitive: parameters are literal XAML text.
    public void IsPaletteSelected_ForVariousPalettesAndParameters_ShouldMatchTheEnumNameExactly(
        Palette selected, string? parameterText, bool expected)
    {
        // Arrange & Act
        var isSelected = PresentationMath.IsPaletteSelected(selected, parameterText);

        // Assert
        isSelected.Should().Be(expected);
    }

    [Theory]
    [InlineData(ThemeMode.Dark, "Dark", true)]
    [InlineData(ThemeMode.Light, "Dark", false)]
    [InlineData(ThemeMode.System, "System", true)]
    [InlineData(ThemeMode.Dark, null, false)]
    public void IsThemeModeSelected_ForVariousModesAndParameters_ShouldMatchTheEnumNameExactly(
        ThemeMode selected, string? parameterText, bool expected)
    {
        // Arrange & Act
        var isSelected = PresentationMath.IsThemeModeSelected(selected, parameterText);

        // Assert
        isSelected.Should().Be(expected);
    }
}
