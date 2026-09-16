using System.Globalization;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// True when a Likert response equals the fixed value (1-5) passed as the converter parameter.
/// Drives which of the five buttons on a <see cref="TestItem"/>'s row shows as selected, via a
/// <c>DataTrigger</c> in <c>TestPage.xaml</c>. Thin adapter over <see cref="PresentationMath.IsLikertSelected"/>;
/// this file implements <c>IValueConverter</c>, a MAUI type, so it cannot be linked into the test
/// project and the actual comparison lives in the MAUI-free <c>PresentationMath</c> instead.
/// </summary>
public sealed class LikertSelectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        PresentationMath.IsLikertSelected(value as int?, parameter as string);

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
