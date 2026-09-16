namespace CoreChoice.Services;

/// <summary>
/// The MAUI-free surface of <see cref="ThemeService"/>: just enough for
/// <see cref="CoreChoice.Presentation.SettingsViewModel"/> to read the current appearance and
/// persist a new one, without that view model ever referencing a <c>Microsoft.Maui.*</c> type.
/// <see cref="ThemeService"/> implements this directly; <c>CoreChoice.App.Tests</c> links
/// <see cref="CoreChoice.Presentation.SettingsViewModel"/> in by source (see that project's
/// <c>Compile Include</c> entries) and stands in a plain test double for this interface instead,
/// the same shape <see cref="AppearanceChoice"/> already exists to support.
/// </summary>
public interface IThemeStore
{
    /// <summary>The appearance currently in effect (and already persisted).</summary>
    AppearanceChoice Current { get; }

    /// <summary>Persists <paramref name="choice"/> and applies it immediately, everywhere.</summary>
    void Set(AppearanceChoice choice);
}
