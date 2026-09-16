namespace CoreChoice.Services;

/// <summary>
/// Turns the backend's confidence integer into the four words a person actually sees. The
/// number itself never reaches the screen: to someone already prone to over-deciding, "75%"
/// reads as a one-in-four chance of ruining their life, and sends them back to searching.
/// Four labels replace the percentage instead.
/// </summary>
public static class VerdictLabel
{
    /// <summary>
    /// The label for a confidence value. The backend is expected to send 0..100, and anything
    /// outside that saturates to the nearest band rather than throwing — the bands are
    /// open-ended at both ends, so a negative reads as "Genuinely close" and anything above 100
    /// as "Clear direction". A malformed number degrades instead of crashing the screen.
    /// </summary>
    public static string For(int confidence) => confidence switch
    {
        >= 80 => "Clear direction",
        >= 65 => "Strong case",
        >= 50 => "Slight edge",
        _ => "Genuinely close",
    };

    /// <summary>
    /// Whether a screen should show this verdict with accent emphasis. The line is 65 — the
    /// same boundary that already separates "Strong case"/"Clear direction" from "Slight
    /// edge"/"Genuinely close" — so emphasis tracks an existing band instead of a fifth line
    /// invented just for this.
    /// </summary>
    public static bool IsStrong(int confidence) => confidence >= 65;
}
