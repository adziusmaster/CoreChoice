using CoreChoice.Services;
using FluentAssertions;

namespace CoreChoice.App.Tests.Services;

public class AppearanceChoiceTests
{
    [Fact]
    public void Default_ShouldBeConsideredDark()
    {
        // Arrange & Act
        var choice = AppearanceChoice.Default;

        // Assert
        choice.Palette.Should().Be(Palette.Considered);
        choice.Mode.Should().Be(ThemeMode.Dark);
    }

    [Theory]
    [InlineData(Palette.Considered, ThemeMode.Dark,  "Tokens.Considered.Dark")]
    [InlineData(Palette.Composed,   ThemeMode.Light, "Tokens.Composed.Light")]
    [InlineData(Palette.Still,      ThemeMode.Dark,  "Tokens.Still.Dark")]
    public void DictionaryName_ShouldNameTheResourceFile(Palette p, ThemeMode m, string expected)
    {
        // Arrange
        var choice = new AppearanceChoice(p, m);

        // Act
        var name = choice.DictionaryName;

        // Assert — this string IS the filename; a mismatch renders an unstyled screen.
        name.Should().Be(expected);
    }

    [Fact]
    public void DictionaryName_ForSystemMode_ShouldRequireAResolvedMode()
    {
        // Arrange — System is a preference, not a dictionary. Resolving it needs the OS,
        // so asking for its filename directly is a programming error rather than a default.
        var choice = new AppearanceChoice(Palette.Considered, ThemeMode.System);

        // Act
        var act = () => choice.DictionaryName;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0, 2, Palette.Considered, ThemeMode.Dark)]
    [InlineData(1, 1, Palette.Composed,   ThemeMode.Light)]
    [InlineData(2, 0, Palette.Still,      ThemeMode.System)]
    public void FromStored_ForValuesInRange_ShouldRoundTripTheChoice(
        int palette, int mode, Palette expectedPalette, ThemeMode expectedMode)
    {
        // Arrange & Act
        var choice = AppearanceChoice.FromStored(palette, mode);

        // Assert
        choice.Palette.Should().Be(expectedPalette);
        choice.Mode.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData(7, 1)]
    [InlineData(-1, 1)]
    public void FromStored_WhenPaletteNamesNoMember_ShouldFallBackWithoutTouchingMode(
        int palette, int mode)
    {
        // Arrange & Act — a palette removed in a later build, or a downgraded install,
        // leaves an integer in storage that no member answers to.
        var choice = AppearanceChoice.FromStored(palette, mode);

        // Assert — the bad field falls back; the good one is kept.
        choice.Palette.Should().Be(AppearanceChoice.Default.Palette);
        choice.Mode.Should().Be(ThemeMode.Light);
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(1, -4)]
    public void FromStored_WhenModeNamesNoMember_ShouldFallBackWithoutTouchingPalette(
        int palette, int mode)
    {
        // Arrange & Act
        var choice = AppearanceChoice.FromStored(palette, mode);

        // Assert
        choice.Mode.Should().Be(AppearanceChoice.Default.Mode);
        choice.Palette.Should().Be(Palette.Composed);
    }

    [Fact]
    public void FromStored_ForAnyIntegerPair_ShouldNeverProduceAnUndefinedEnum()
    {
        // Arrange — the point of the sweep is the values OUTSIDE each enum's range. Iterating
        // only defined members would assert that valid input stays valid, which the cast gives
        // for free and which no bug could ever break.
        var range = Enumerable.Range(-3, 14).ToArray();   // -3..10, spanning both enums' 0..2

        // Act & Assert
        foreach (var palette in range)
            foreach (var mode in range)
            {
                var choice = AppearanceChoice.FromStored(palette, mode);
                Enum.IsDefined(choice.Palette).Should()
                    .BeTrue($"palette {palette} must not survive as an undefined enum");
                Enum.IsDefined(choice.Mode).Should()
                    .BeTrue($"mode {mode} must not survive as an undefined enum");
            }
    }

}
