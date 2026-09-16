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
    /// The label for a confidence value. The backend is expected to send 0..100, but a
    /// malformed response is clamped into that range rather than thrown on, so a bad number
    /// degrades to the nearest valid band instead of crashing the screen.
    /// </summary>
    public static string For(int confidence)
    {
        var clamped = Math.Clamp(confidence, 0, 100);

        return clamped switch
        {
            >= 80 => "Clear direction",
            >= 65 => "Strong case",
            >= 50 => "Slight edge",
            _ => "Genuinely close",
        };
    }

    /// <summary>
    /// Whether a screen should show this verdict with accent emphasis. The line is 65 — the
    /// same boundary that already separates "Strong case"/"Clear direction" from "Slight
    /// edge"/"Genuinely close" — so emphasis tracks an existing band instead of a fifth line
    /// invented just for this.
    /// </summary>
    public static bool IsStrong(int confidence) => Math.Clamp(confidence, 0, 100) >= 65;
}
