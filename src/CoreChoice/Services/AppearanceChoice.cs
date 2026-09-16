namespace CoreChoice.Services;

public enum Palette { Considered, Composed, Still }

public enum ThemeMode { System, Light, Dark }

/// <summary>
/// What the person chose in Settings: a palette and a mode. The pair names exactly one token
/// dictionary, which is what makes adding a fourth palette a new file and no code.
/// </summary>
public sealed record AppearanceChoice(Palette Palette, ThemeMode Mode)
{
    public static AppearanceChoice Default => new(Palette.Considered, ThemeMode.Dark);

    /// <summary>
    /// Rebuilds a choice from the two integers held in storage, falling back per-field when a
    /// value names no enum member. Stored preferences outlive the code that wrote them: a
    /// removed palette or a downgraded install would otherwise cast straight to an undefined
    /// enum and silently resolve to the wrong dictionary.
    /// </summary>
    public static AppearanceChoice FromStored(int palette, int mode) => new(
        Enum.IsDefined(typeof(Palette), palette) ? (Palette)palette : Default.Palette,
        Enum.IsDefined(typeof(ThemeMode), mode) ? (ThemeMode)mode : Default.Mode);

    /// <summary>
    /// The resource dictionary this choice resolves to. Throws for <see cref="ThemeMode.System"/>:
    /// that is a preference, not a dictionary, and resolving it requires asking the OS.
    /// </summary>
    public string DictionaryName => Mode switch
    {
        ThemeMode.Light => $"Tokens.{Palette}.Light",
        ThemeMode.Dark => $"Tokens.{Palette}.Dark",
        _ => throw new InvalidOperationException(
            "System mode must be resolved to Light or Dark before naming a dictionary."),
    };

    public AppearanceChoice Resolve(bool systemIsDark) =>
        Mode == ThemeMode.System
            ? this with { Mode = systemIsDark ? ThemeMode.Dark : ThemeMode.Light }
            : this;
}
