using CoreChoice.Presentation;
using CoreChoice.Services;
using FluentAssertions;
using NSubstitute;

namespace CoreChoice.App.Tests.Presentation;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_WhenTheStoreAlreadyHoldsAChoice_ShouldExposeItAsSelected()
    {
        // Arrange
        var store = Substitute.For<IThemeStore>();
        store.Current.Returns(new AppearanceChoice(Palette.Still, ThemeMode.Light));

        // Act
        var vm = new SettingsViewModel(store);

        // Assert
        vm.SelectedPalette.Should().Be(Palette.Still);
        vm.SelectedMode.Should().Be(ThemeMode.Light);
    }

    [Fact]
    public void Select_Palette_ShouldPersistThroughTheThemeStore()
    {
        // Arrange
        var store = Substitute.For<IThemeStore>();
        store.Current.Returns(new AppearanceChoice(Palette.Considered, ThemeMode.Dark));
        var vm = new SettingsViewModel(store);

        // Act
        vm.Select(Palette.Composed);

        // Assert
        vm.SelectedPalette.Should().Be(Palette.Composed);
        store.Received(1).Set(new AppearanceChoice(Palette.Composed, ThemeMode.Dark));
    }

    [Fact]
    public void Select_Mode_ShouldPersistThroughTheThemeStore()
    {
        // Arrange
        var store = Substitute.For<IThemeStore>();
        store.Current.Returns(new AppearanceChoice(Palette.Considered, ThemeMode.Dark));
        var vm = new SettingsViewModel(store);

        // Act
        vm.Select(ThemeMode.System);

        // Assert
        vm.SelectedMode.Should().Be(ThemeMode.System);
        store.Received(1).Set(new AppearanceChoice(Palette.Considered, ThemeMode.System));
    }

    [Fact]
    public void Select_Mode_ShouldKeepTheAlreadyChosenPaletteRatherThanResettingIt()
    {
        // Arrange — choosing a mode must not silently discard an earlier palette choice.
        var store = Substitute.For<IThemeStore>();
        store.Current.Returns(new AppearanceChoice(Palette.Considered, ThemeMode.Dark));
        var vm = new SettingsViewModel(store);
        vm.Select(Palette.Still);

        // Act
        vm.Select(ThemeMode.Light);

        // Assert
        store.Received(1).Set(new AppearanceChoice(Palette.Still, ThemeMode.Light));
    }

    [Fact]
    public void Palettes_ShouldOfferConsideredComposedAndStillWithDistinctSwatchColours()
    {
        // Arrange
        var store = Substitute.For<IThemeStore>();
        store.Current.Returns(AppearanceChoice.Default);

        // Act
        var vm = new SettingsViewModel(store);

        // Assert
        vm.Palettes.Select(p => p.Value).Should().BeEquivalentTo(
            [Palette.Considered, Palette.Composed, Palette.Still], o => o.WithStrictOrdering());
        vm.Palettes.Select(p => p.GroundHex).Should().OnlyHaveUniqueItems();
        vm.Palettes.Select(p => p.AccentHex).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(Palette.Considered, "Clear and awake")]
    [InlineData(Palette.Composed, "Cool and quiet")]
    [InlineData(Palette.Still, "Soft and low-contrast")]
    public void Palettes_ShouldCarryTheApprovedCopyExactly(Palette palette, string expectedDescription)
    {
        // Arrange
        var store = Substitute.For<IThemeStore>();
        store.Current.Returns(AppearanceChoice.Default);

        // Act
        var vm = new SettingsViewModel(store);

        // Assert
        vm.Palettes.Single(p => p.Value == palette).Description.Should().Be(expectedDescription);
    }
}
