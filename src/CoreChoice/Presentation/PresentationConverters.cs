using System.Globalization;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// True when a Likert response equals the fixed value (1-5) passed as the converter parameter.
/// Drives which of the five buttons on a <see cref="TestItem"/>'s row shows as selected, via a
/// <c>DataTrigger</c> in <c>TestPage.xaml</c>.
/// </summary>
public sealed class LikertSelectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int response
        && parameter is string s
        && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected)
        && response == expected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Formats the header on <c>TestPage</c>: the item after the last answered one, of the
/// fixed total. "AnsweredCount" answered means the person is now looking at item AnsweredCount+1.</summary>
public sealed class TestPositionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int answered
            ? $"{Math.Min(answered + 1, IpipItemBank.ItemCount)} of {IpipItemBank.ItemCount}"
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Capitalizes <see cref="TraitScore.Band"/> ("low"/"moderate"/"high") for display.</summary>
public sealed class TraitBandLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TraitScore score
            ? culture.TextInfo.ToTitleCase(score.Band)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Positions a trait's dot proportionally along its line on <c>ProfilePage</c>, mirroring the
/// artboard's <c>left: calc(N% - 5px)</c>. An <see cref="AbsoluteLayout"/> child with the
/// <c>PositionProportional</c> layout flag reads X as a 0-1 fraction of the remaining track
/// width, which is exactly that formula for a 10px-wide dot.
/// </summary>
public sealed class TraitDotBoundsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TraitScore score
            ? new Rect(score.Value / 100.0, 0.5, 10, 10)
            : new Rect(0, 0.5, 10, 10);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when a bound string is non-null and non-empty. Used to show the coin-grant
/// banner on <c>ProfilePage</c> only when <c>ProfileViewModel.GrantMessage</c> is set.</summary>
public sealed class IsNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
