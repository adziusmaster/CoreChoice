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
}
