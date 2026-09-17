using CoreChoice.Platforms.Android;
using CoreChoice.Resources.Styles;

namespace CoreChoice.Services;

/// <summary>
/// Owns appearance. Swaps the single token dictionary every screen references through
/// DynamicResource, and remembers the choice across launches.
/// </summary>
public sealed class ThemeService : IThemeStore
{
    private const string PaletteKey = "corechoice_palette";
    private const string ModeKey = "corechoice_mode";

    private ResourceDictionary? _active;

    public AppearanceChoice Current { get; private set; }

    public ThemeService()
    {
        Current = AppearanceChoice.FromStored(
            Preferences.Default.Get(PaletteKey, (int)AppearanceChoice.Default.Palette),
            Preferences.Default.Get(ModeKey, (int)AppearanceChoice.Default.Mode));

        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.RequestedThemeChanged += (_, _) => { if (Current.Mode == ThemeMode.System) Apply(); };
    }

    public void Set(AppearanceChoice choice)
    {
        Current = choice;
        Preferences.Default.Set(PaletteKey, (int)choice.Palette);
        Preferences.Default.Set(ModeKey, (int)choice.Mode);
        Apply();
    }

    public void Apply()
    {
        if (Microsoft.Maui.Controls.Application.Current is not { } app) return;

        app.UserAppTheme = Current.Mode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

        var systemIsDark = app.RequestedTheme == AppTheme.Dark;
        var resolved = Current.Resolve(systemIsDark);
        var wanted = Build(resolved);

        if (_active is not null)
            app.Resources.MergedDictionaries.Remove(_active);
        app.Resources.MergedDictionaries.Add(wanted);
        _active = wanted;

        // The system status/navigation bars are transparent from launch (set once in
        // MainActivity) so the app's own background shows through them instead of a black band —
        // but that only follows the palette if the window's background and the bar icons' light/
        // dark appearance are kept in step with it here, on every apply, not just at startup.
        if (app.Resources.TryGetValue("Bg", out var bgResource) && bgResource is Color bg)
            SystemBarAppearance.Apply(bg, resolved.Mode == ThemeMode.Light);
    }

    private static ResourceDictionary Build(AppearanceChoice resolved) => resolved.DictionaryName switch
    {
        "Tokens.Considered.Dark" => new TokensConsideredDark(),
        "Tokens.Considered.Light" => new TokensConsideredLight(),
        "Tokens.Composed.Dark" => new TokensComposedDark(),
        "Tokens.Composed.Light" => new TokensComposedLight(),
        "Tokens.Still.Dark" => new TokensStillDark(),
        "Tokens.Still.Light" => new TokensStillLight(),
        _ => new TokensConsideredDark(),
    };
}
