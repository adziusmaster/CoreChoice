using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>One row on the appearance screen: a palette's name, its one-line description (copy
/// approved verbatim — see <see cref="SettingsViewModel.Palettes"/>), and the two reference
/// colours its swatch shows. <see cref="GroundHex"/> and <see cref="AccentHex"/> are fixed
/// preview colours for that palette's dark form regardless of the mode currently in effect — the
/// swatch's job is to show what each palette looks like, not to follow the very choice it is
/// offering to make.</summary>
public sealed record PaletteOption(Palette Value, string Name, string Description, string GroundHex, string AccentHex);

/// <summary>
/// Backs the appearance screen (artboard 9 · "Appearance") — the only place in the app a palette
/// or light/dark/system choice can be made. No MAUI type appears anywhere in this file:
/// <c>CoreChoice.App.Tests</c> links it in by source, same discipline as every other view model in
/// this app (see <c>PresentationMath.cs</c>'s doc comment). <see cref="ThemeService"/> itself uses
/// MAUI types throughout (<c>Preferences</c>, <c>Application.Current</c>), so this view model
/// depends on <see cref="IThemeStore"/>, its MAUI-free surface, instead.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeStore _themeStore;

    /// <summary>The three palettes offered today, in display order. Copy is approved verbatim;
    /// the hex pair is each palette's own dark-form ground and accent, used only to paint its
    /// swatch — see <see cref="PaletteOption"/>'s own doc.</summary>
    public IReadOnlyList<PaletteOption> Palettes { get; } =
    [
        new(Palette.Considered, "Considered", "Clear and awake", "#0A0D0D", "#4FD1C5"),
        new(Palette.Composed, "Composed", "Cool and quiet", "#0E1114", "#82A9DC"),
        new(Palette.Still, "Still", "Soft and low-contrast", "#101017", "#B7A6E9"),
    ];

    // Named accessors rather than indexer bindings ("Palettes[0]") on the page: this project
    // compiles XAML bindings ahead of time (MauiXamlInflator=SourceGen), and an indexer path is
    // exactly the kind of binding that fails silently at runtime rather than at build time — the
    // one failure mode this app cannot afford to risk on a screen with no device to catch it on.
    public PaletteOption ConsideredOption => Palettes[0];
    public PaletteOption ComposedOption => Palettes[1];
    public PaletteOption StillOption => Palettes[2];

    [ObservableProperty]
    private Palette selectedPalette;

    [ObservableProperty]
    private ThemeMode selectedMode;

    /// <summary>Reads whatever <paramref name="themeStore"/> already holds, so the screen opens
    /// with the correct row and mode already marked — never a blank or default-looking picker for
    /// someone who chose something else last time.</summary>
    public SettingsViewModel(IThemeStore themeStore)
    {
        _themeStore = themeStore;
        selectedPalette = themeStore.Current.Palette;
        selectedMode = themeStore.Current.Mode;
    }

    /// <summary>Chooses a palette, keeping the current mode, and persists it immediately through
    /// <see cref="IThemeStore.Set"/> — which is also what applies it to every screen already on
    /// the navigation stack, not just this one.</summary>
    public void Select(Palette palette)
    {
        SelectedPalette = palette;
        _themeStore.Set(new AppearanceChoice(palette, SelectedMode));
    }

    /// <summary>Chooses a mode, keeping the current palette, and persists it the same way.</summary>
    public void Select(ThemeMode mode)
    {
        SelectedMode = mode;
        _themeStore.Set(new AppearanceChoice(SelectedPalette, mode));
    }
}
