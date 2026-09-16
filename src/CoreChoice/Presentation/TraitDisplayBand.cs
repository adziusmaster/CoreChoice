using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// The five-level band the result screen shows next to each trait row ("Very high" down to
/// "Very low"). This is display-only: <see cref="TraitScore.Band"/> stays the coarse three-level
/// band the server's prompt assembly depends on, and nothing here feeds that path — a screen and
/// a prompt are allowed to want different resolutions of the same number.
///
/// Boundaries are symmetric around the midpoint (50): 15 and 85 mark the outer step away from the
/// centre, 35 and 65 the inner one. The same 15-point distance is what <see cref="ProfileNoteComposer"/>
/// treats as "meaningfully far from the middle" — a trait is non-"Moderate" here exactly when it
/// is salient there.
/// </summary>
public static class TraitDisplayBand
{
    public static string DisplayBand(this TraitScore score) => score.Value switch
    {
        <= 15 => "Very low",
        <= 35 => "Low",
        <= 65 => "Moderate",
        <= 85 => "High",
        _ => "Very high",
    };
}
