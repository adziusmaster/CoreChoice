namespace CoreChoice.Application;

/// <summary>
/// One pack's raw price lookup outcome, before the all-or-nothing decision in
/// <see cref="StorePriceResolver"/>. <see cref="FormattedPrice"/> is null, empty, or whitespace
/// whenever the store could not price that pack — the product query failed, the offer list came
/// back empty (e.g. the product is not live in the Play Console yet), or the store returned
/// nothing usable.
/// </summary>
public sealed record PackPriceResolution(string ProductId, string? FormattedPrice);

/// <summary>
/// The all-or-nothing decision behind the coins screen's placeholder-prices banner. Pulled out of
/// <c>PlayBillingService</c> — which touches <c>Android.*</c> types throughout and so cannot be
/// linked into <c>CoreChoice.App.Tests</c> or exercised on a machine with no device — into this
/// MAUI-free helper so the one rule that matters here actually has test coverage.
///
/// <para>Google Play's <c>FormattedPrice</c> is the localised, tax-inclusive string the person is
/// actually charged, so it is the only figure that is correct in every country (VAT rates differ
/// by market). A placeholder price is a guess. Showing one pack's placeholder next to two real
/// prices would let that guess pass for a real one, with nothing on screen to tell them apart —
/// worse than showing no real price at all, because the person believes it. So the rule is
/// binary: either <b>every</b> pack got a real price from the store, or the whole screen falls
/// back to the placeholder set and says so. Partial mixes are impossible by construction here,
/// not by convention at each call site.</para>
/// </summary>
public static class StorePriceResolver
{
    /// <summary>
    /// Resolves the packs to show, given each pack's raw price lookup outcome.
    /// </summary>
    /// <param name="fallbackPacks">The placeholder set to fall back to, and also what defines
    /// which product ids must be priced for the result to count as "from the store".</param>
    /// <param name="resolutions">One outcome per pack the store was asked to price. A pack with no
    /// matching entry counts the same as one whose price could not be resolved.</param>
    /// <returns>Either <paramref name="fallbackPacks"/> itself with <c>PricesAreFromStore: false</c>,
    /// or the resolved packs (same order as <paramref name="fallbackPacks"/>, prices swapped in)
    /// with <c>PricesAreFromStore: true</c>.</returns>
    public static (IReadOnlyList<AnalysisPack> Packs, bool PricesAreFromStore) Resolve(
        IReadOnlyList<AnalysisPack> fallbackPacks,
        IReadOnlyList<PackPriceResolution> resolutions)
    {
        var priceByProductId = new Dictionary<string, string>();
        foreach (var resolution in resolutions)
        {
            if (string.IsNullOrWhiteSpace(resolution.FormattedPrice))
                return (fallbackPacks, false);

            priceByProductId[resolution.ProductId] = resolution.FormattedPrice;
        }

        var resolved = new List<AnalysisPack>(fallbackPacks.Count);
        foreach (var pack in fallbackPacks)
        {
            if (!priceByProductId.TryGetValue(pack.ProductId, out var price))
                return (fallbackPacks, false);

            resolved.Add(pack with { DisplayPrice = price });
        }

        return (resolved, true);
    }
}
