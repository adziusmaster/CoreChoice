using CoreChoice.Application;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// One row on the Answers tab. Carries only what a row needs to be recognised — both options,
/// when it was asked, and the verdict label — never the raw confidence number: this app's rule
/// that a percentage never reaches a screen (see <see cref="VerdictLabel"/>'s own doc) applies to
/// every screen that shows a past decision, not only the one it was first answered on.
/// </summary>
public sealed record PastDecisionRow(long Id, string OptionA, string OptionB, string AskedAtDisplay, string Verdict)
{
    public static PastDecisionRow From(PastDecision decision) => new(
        decision.Id,
        decision.Dilemma.OptionA,
        decision.Dilemma.OptionB,
        FormatAskedAt(decision.AskedAt),
        VerdictLabel.For(decision.Analysis.Confidence));

    /// <summary>
    /// Formats directly off the stored <see cref="DateTimeOffset"/> rather than converting to
    /// local time first, so this is deterministic under test regardless of the machine's time
    /// zone; the device's own clock/locale settings already put whatever offset is correct into
    /// <see cref="PastDecision.AskedAt"/> at the moment it was recorded.
    /// </summary>
    private static string FormatAskedAt(DateTimeOffset askedAt) =>
        askedAt.ToString("MMM d, yyyy 'at' h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
}
