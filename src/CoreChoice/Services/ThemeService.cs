using CoreChoice.Resources.Styles;

namespace CoreChoice.Services;

/// <summary>
/// Owns appearance. Swaps the single token dictionary every screen references through
/// DynamicResource, and remembers the choice across launches.
/// </summary>
public sealed class ThemeService
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
        var wanted = Build(Current.Resolve(systemIsDark));

        if (_active is not null)
            app.Resources.MergedDictionaries.Remove(_active);
        app.Resources.MergedDictionaries.Add(wanted);
        _active = wanted;
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
