using System.Globalization;
using CoreChoice.Domain;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// True when a Likert response equals the fixed value (1-5) passed as the converter parameter.
/// Drives which of a <see cref="TestItem"/> row's two stacked Buttons per column (the plain one and
/// the "selected" one, painted with ordinary — not trigger — <c>DynamicResource</c> properties) is
/// visible, via <c>IsVisible</c> in <c>TestPage.xaml</c>. Thin adapter over
/// <see cref="PresentationMath.IsLikertSelected"/>; this file implements <c>IValueConverter</c>, a
/// MAUI type, so it cannot be linked into the test project and the actual comparison lives in the
/// MAUI-free <c>PresentationMath</c> instead. A parameter starting with <c>"!"</c> negates the
/// result, same convention as <see cref="PaletteSelectedConverter"/>.
/// </summary>
public sealed class LikertSelectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (negate, text) = PaletteSelectedConverter.SplitNegation(parameter as string);
        var selected = PresentationMath.IsLikertSelected(value as int?, text);
        return negate ? !selected : selected;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats the header on <c>TestPage</c>: the item after the last answered one, of the
/// fixed total. "AnsweredCount" answered means the person is now looking at item AnsweredCount+1.
/// Thin adapter over <see cref="PresentationMath.FormatTestPosition"/>.</summary>
public sealed class TestPositionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int answered
            ? PresentationMath.FormatTestPosition(answered, IpipItemBank.ItemCount)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Renders a trait's five-level <see cref="TraitDisplayBand"/> ("Very low" .. "Very high")
/// for the result screen. Deliberately not <see cref="TraitScore.Band"/> — that coarser band feeds
/// the server prompt, not this screen.</summary>
public sealed class TraitBandLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TraitScore score
            ? score.DisplayBand()
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Positions a trait's dot proportionally along its line on <c>ProfilePage</c>. An
/// <see cref="AbsoluteLayout"/> child with the <c>PositionProportional</c> layout flag reads X as a
/// 0-1 fraction of the remaining track width. Thin adapter over
/// <see cref="PresentationMath.TraitDotBounds"/> — see that method's doc for how this relates to
/// (and deliberately differs from) the artboard's own <c>calc(N% - 5px)</c> formula. This file
/// uses <c>Rect</c>, a MAUI type, so it cannot be linked into the test project; the maths itself
/// lives in the MAUI-free <c>PresentationMath</c> and is covered there.
/// </summary>
public sealed class TraitDotBoundsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bounds = PresentationMath.TraitDotBounds(value is TraitScore score ? score.Value : 0);
        return new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when a bound string is non-null and non-empty. Used to show the coin-grant
/// banner on <c>ProfilePage</c> only when <c>ProfileViewModel.GrantMessage</c> is set. Thin adapter
/// over <see cref="PresentationMath.IsNotEmpty"/>.</summary>
public sealed class IsNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        PresentationMath.IsNotEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Turns a <c>"#RRGGBB"</c> string into a <see cref="Color"/> for a palette swatch on
/// <c>SettingsPage</c> — <see cref="SettingsViewModel.Palettes"/>'s <c>GroundHex</c> and
/// <c>AccentHex</c> are fixed reference colours for each palette's own preview, deliberately not
/// <c>DynamicResource</c> tokens (a swatch that followed the current theme could never show what
/// the *other* two palettes look like). This file uses <c>Color</c>, a MAUI type, so — like every
/// other converter here — it cannot be linked into the test project; there is nothing to unit-test
/// in a one-line call into MAUI's own <see cref="Microsoft.Maui.Graphics.Color.FromArgb(string)"/>.
/// </summary>
public sealed class HexColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex ? Color.FromArgb(hex) : Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when a bound <see cref="Palette"/> matches the converter parameter's name.
/// Thin adapter over <see cref="PresentationMath.IsPaletteSelected"/>, the same shape as
/// <see cref="LikertSelectedConverter"/> above. A parameter starting with <c>"!"</c> negates the
/// result — used to drive the "unselected" half of a swapped-Border pair (see
/// <c>SettingsPage.xaml</c>'s palette rows) with an ordinary <c>IsVisible</c> binding instead of a
/// colour-carrying <c>DataTrigger</c>, which is what silently keeps a stale colour across a theme
/// switch (a <c>DynamicResource</c> used as a trigger setter value resolves once, when the trigger
/// fires, and does not re-resolve when <c>ThemeService</c> swaps the token dictionary).</summary>
public sealed class PaletteSelectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (negate, name) = SplitNegation(parameter as string);
        var selected = value is Palette p && PresentationMath.IsPaletteSelected(p, name);
        return negate ? !selected : selected;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static (bool Negate, string? Name) SplitNegation(string? parameter) =>
        parameter is not null && parameter.StartsWith('!') ? (true, parameter[1..]) : (false, parameter);
}

/// <summary>Same idea for the mode segmented control. Thin adapter over
/// <see cref="PresentationMath.IsThemeModeSelected"/>. Supports the same <c>"!"</c>-prefix negation
/// as <see cref="PaletteSelectedConverter"/>, for the same reason.</summary>
public sealed class ThemeModeSelectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (negate, name) = PaletteSelectedConverter.SplitNegation(parameter as string);
        var selected = value is ThemeMode m && PresentationMath.IsThemeModeSelected(m, name);
        return negate ? !selected : selected;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Negates a bound bool. Used to show the "unselected" half of a swapped-element pair
/// (e.g. <c>AnalysisPage.xaml</c>'s verdict border) via an ordinary <c>IsVisible</c> binding rather
/// than a colour-carrying <c>DataTrigger</c> — see <see cref="PaletteSelectedConverter"/>'s doc for
/// why that trigger pattern goes stale across a theme switch.</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value ?? false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value ?? false;
}
