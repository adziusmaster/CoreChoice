namespace CoreChoice.Application;

/// <summary>A purchasable pack of analyses, mapped to a Google Play product id.</summary>
public sealed record AnalysisPack(string ProductId, int Analyses, string DisplayPrice);

/// <summary>A completed, deliberately UN-CONSUMED purchase, to be redeemed with the backend.</summary>
public sealed record PurchaseTicket(string ProductId, string PurchaseToken);

/// <summary>
/// The platform store. Behind an interface so the view model and redemption logic stay
/// platform-agnostic and testable.
/// </summary>
public interface IBillingService
{
    /// <summary>False on a build or device where billing is unavailable. The UI hides buying entirely.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Packs with placeholder price labels, used only when the store cannot be reached — offline,
    /// billing unavailable, or the products are not live in Play yet.
    /// </summary>
    IReadOnlyList<AnalysisPack> FallbackPacks { get; }

    /// <summary>
    /// Packs with prices resolved from the store: Google Play's localised, tax-inclusive
    /// FormattedPrice, i.e. the exact string the user is charged at checkout. VAT rates differ by
    /// country, so this is the only way a displayed price can be correct everywhere. Falls back to
    /// the matching <see cref="FallbackPacks"/> label per pack whose price cannot be fetched.
    /// </summary>
    Task<IReadOnlyList<AnalysisPack>> GetPacksAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs the purchase flow. Returns null when the person cancelled. The purchase is left
    /// UN-CONSUMED on purpose: the caller redeems it with the backend first and calls
    /// <see cref="ConsumeAsync"/> only once the coins are actually granted. Consuming first means a
    /// paid purchase can vanish if the grant call fails.
    /// </summary>
    Task<PurchaseTicket?> BuyAsync(string productId, CancellationToken ct = default);

    /// <summary>Consumes a redeemed purchase so the product can be bought again.</summary>
    Task ConsumeAsync(string purchaseToken, CancellationToken ct = default);
}
