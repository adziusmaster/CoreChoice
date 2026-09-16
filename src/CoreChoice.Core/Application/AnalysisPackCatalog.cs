namespace CoreChoice.Application;

/// <summary>
/// The single source of truth for the three purchasable analysis packs' product ids and analysis
/// counts. Before this existed, <c>CoinsViewModel</c> and <c>PlayBillingService</c> each listed the
/// three ids independently, with nothing to stop the two lists drifting apart — a typo in either
/// one would mean a pack the store can price never gets matched to the row the person sees, or
/// vice versa. Both now read from here instead, so there is only one list to update when the Play
/// Console catalog ever changes, and the two call sites cannot disagree by construction.
/// </summary>
public static class AnalysisPackCatalog
{
    public const string TenAnalysesProductId = "corechoice.analyses.10";
    public const string ThirtyAnalysesProductId = "corechoice.analyses.30";
    public const string HundredAnalysesProductId = "corechoice.analyses.100";

    /// <summary>Product id paired with how many analyses that pack grants. Declared in the order
    /// the coins screen shows them (smallest to largest).</summary>
    public static IReadOnlyList<(string ProductId, int Analyses)> Entries { get; } =
    [
        (TenAnalysesProductId, 10),
        (ThirtyAnalysesProductId, 30),
        (HundredAnalysesProductId, 100),
    ];
}
