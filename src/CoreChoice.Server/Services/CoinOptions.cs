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

    /// <summary>Free grants allowed per hashed origin inside <see cref="OriginWindow"/>.</summary>
    public int MaxGrantsPerOrigin { get; set; } = 5;

    public TimeSpan OriginWindow { get; set; } = TimeSpan.FromDays(7);
}
