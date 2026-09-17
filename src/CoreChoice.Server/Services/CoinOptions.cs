namespace CoreChoice.Server.Services;

/// <summary>
/// The economy's numbers. Configuration rather than constants because these get tuned against real
/// behaviour, and tuning them should not require a release.
/// </summary>
internal sealed class CoinOptions
{
    public const string SectionName = "Coins";

    /// <summary>Granted on a device's first contact, before any test. Pays for exploring.</summary>
    public int FirstContactGrant { get; set; } = 5;

    /// <summary>Granted once when a person finishes the Big Five test. The test pays them, never the reverse.</summary>
    public int ProfileCompletionGrant { get; set; } = 5;

    /// <summary>Cost of one analysis. Generic and personalized cost the same: same Gemini call.</summary>
    public int AnalysisPrice { get; set; } = 1;

    /// <summary>
    /// Free grants allowed per hashed origin inside <see cref="OriginWindow"/>.
    ///
    /// Counted per GRANT, not per person, and each new device takes two of them — the
    /// first-contact seed and the profile-completion bonus. So this number is really "people on
    /// one connection, doubled". At 5 the third person behind a shared router got nothing, with
    /// no explanation on screen: a couple plus a visitor, a family, a small office, or a room of
    /// testers all hit it. 20 allows about ten people per week per address, which still stops a
    /// script farming free analyses while leaving ordinary shared connections alone.
    /// </summary>
    public int MaxGrantsPerOrigin { get; set; } = 20;

    public TimeSpan OriginWindow { get; set; } = TimeSpan.FromDays(7);
}
