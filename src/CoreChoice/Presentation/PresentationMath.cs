using System.Globalization;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// The actual calculations behind every converter in <c>PresentationConverters.cs</c>, pulled out
/// into a MAUI-free file on purpose. That file implements <c>IValueConverter</c> and uses
/// <c>Rect</c>, both <c>Microsoft.Maui.*</c> types — and <c>CoreChoice.App.Tests</c> targets plain
/// <c>net10.0</c>, so it cannot reference the <c>net10.0-android</c> app project and instead links
/// individual source files in. A file that touches any MAUI type cannot be linked, so it cannot be
/// tested at all: every converter's logic was zero-coverage, including the dot-position maths that
/// places a mark on every trait row on the profile screen. Each converter is now a thin adapter
/// that calls one of the methods below and wraps the result in whatever MAUI type its binding
/// target expects; the methods themselves take and return only plain types, so this file can be
/// linked into the test project and the maths covered directly.
/// </summary>
public static class PresentationMath
{
    private const double DotSize = 10;

    /// <summary>Plain-type bounds for the trait dot: an X fraction of the track (0-1), a fixed
    /// vertical centre, and a fixed width/height. Deliberately not a MAUI <c>Rect</c> — see
    /// <see cref="TraitDotBounds"/>.</summary>
    public readonly record struct DotBounds(double X, double Y, double Width, double Height);

    /// <summary>
    /// Proportional bounds for a trait's dot, given its 0-100 score. <c>X</c> is
    /// <c>traitValue / 100.0</c> — a floating-point division, so a score of 0 lands at exactly
    /// <c>0.0</c>, 100 at exactly <c>1.0</c>, and everything between at its exact fraction with no
    /// truncation. (An integer division here, e.g. <c>traitValue / 100</c>, would silently collapse
    /// every score below 100 to <c>0</c> — that is the bug this maths must never reintroduce.)
    ///
    /// This does NOT reproduce the artboard's <c>left: calc(N% - 5px)</c> exactly. That CSS formula
    /// shifts a percentage position left by half the dot's own width. MAUI's
    /// <c>AbsoluteLayoutFlags.PositionProportional</c> already accounts for the element's own size
    /// when resolving an X of 1, so the two agree at the centre but differ by up to 5px at either
    /// extreme. The MAUI behaviour is kept deliberately: under it, the dot can never be clipped by
    /// overflowing its track, which the artboard's own formula does not guarantee.
    /// </summary>
    public static DotBounds TraitDotBounds(int traitValue) =>
        new(traitValue / 100.0, 0.5, DotSize, DotSize);

    /// <summary>True when <paramref name="response"/> equals the integer parsed from
    /// <paramref name="parameterText"/> — the Likert button whose <c>CommandParameter</c> matches
    /// the stored response is the one shown as selected.</summary>
    public static bool IsLikertSelected(int? response, string? parameterText) =>
        response is int r
        && parameterText is not null
        && int.TryParse(parameterText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected)
        && r == expected;

    /// <summary>"{current} of {itemCount}" header text for the test screen. The current item is the
    /// one after the last answered one, capped at <paramref name="itemCount"/> so the header cannot
    /// read past the end once every item has an answer.</summary>
    public static string FormatTestPosition(int answeredCount, int itemCount) =>
        $"{Math.Min(answeredCount + 1, itemCount)} of {itemCount}";

    /// <summary>True for a non-null, non-empty string. Backs the coin-grant banner's visibility on
    /// the profile screen.</summary>
    public static bool IsNotEmpty(string? value) => !string.IsNullOrEmpty(value);

    /// <summary>True when <paramref name="selected"/>'s own enum name matches
    /// <paramref name="parameterText"/> (e.g. <c>"Considered"</c>) — drives which row on the
    /// appearance screen's palette list shows as checked, the same fixed-row shape
    /// <c>CoinsViewModel</c> already uses for its three packs rather than one repeated template.
    /// </summary>
    public static bool IsPaletteSelected(Palette selected, string? parameterText) =>
        parameterText is not null && string.Equals(selected.ToString(), parameterText, StringComparison.Ordinal);

    /// <summary>Same comparison for the mode segmented control (<c>"Dark"</c>, <c>"Light"</c>,
    /// <c>"System"</c>).</summary>
    public static bool IsThemeModeSelected(ThemeMode selected, string? parameterText) =>
        parameterText is not null && string.Equals(selected.ToString(), parameterText, StringComparison.Ordinal);
}
