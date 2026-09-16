using CoreChoice.Application;

namespace CoreChoice.App.Tests;

/// <summary>
/// A configurable <see cref="IBillingService"/> test double for <c>CoinsViewModelTests</c>.
/// Every outcome (<see cref="GetPacksAsync"/>, <see cref="BuyAsync"/>) is set per test via a
/// public field rather than mocked, and every <see cref="ConsumeAsync"/> call is recorded in
/// <see cref="ConsumedTokens"/> — the whole point of this fake is proving directly whether a
/// purchase was consumed, rather than inferring it from some other side effect.
/// </summary>
internal sealed class FakeBillingService : IBillingService
{
    public bool IsSupported { get; set; } = true;

    public IReadOnlyList<AnalysisPack> FallbackPacks { get; set; } =
    [
        new AnalysisPack(AnalysisPackCatalog.TenAnalysesProductId, 10, "€2.99 (estimate)"),
        new AnalysisPack(AnalysisPackCatalog.ThirtyAnalysesProductId, 30, "€5.99 (estimate)"),
        new AnalysisPack(AnalysisPackCatalog.HundredAnalysesProductId, 100, "€14.99 (estimate)"),
    ];

    /// <summary>Set to make <see cref="GetPacksAsync"/> return these instead of throwing.</summary>
    public IReadOnlyList<AnalysisPack>? StorePacks { get; set; }

    /// <summary>Set to make <see cref="GetPacksAsync"/> throw instead of returning.</summary>
    public Exception? GetPacksException { get; set; }

    /// <summary>Set to make <see cref="BuyAsync"/> return this ticket (or null, for "the person
    /// cancelled") instead of throwing.</summary>
    public PurchaseTicket? BuyResult { get; set; }

    /// <summary>Set to make <see cref="BuyAsync"/> throw instead of returning.</summary>
    public Exception? BuyException { get; set; }

    /// <summary>Every purchase token <see cref="ConsumeAsync"/> was called with, in order. Empty
    /// means it was never called — the assertion the consume-ordering tests care about.</summary>
    public List<string> ConsumedTokens { get; } = [];

    public Task<IReadOnlyList<AnalysisPack>> GetPacksAsync(CancellationToken ct = default) =>
        GetPacksException is null
            ? Task.FromResult(StorePacks ?? FallbackPacks)
            : throw GetPacksException;

    public Task<PurchaseTicket?> BuyAsync(string productId, CancellationToken ct = default) =>
        BuyException is null ? Task.FromResult(BuyResult) : throw BuyException;

    public Task ConsumeAsync(string purchaseToken, CancellationToken ct = default)
    {
        // Deliberately honours the token it is handed rather than ignoring it: CoinsViewModel.
        // BuyAsync must finalise a granted purchase with CancellationToken.None, never the
        // caller's own token, or a cancellation racing in right after redemption succeeds would
        // leave a paid, granted purchase un-consumed. Throwing here when the given token is
        // already cancelled is what makes a regression to the wrong token fail a test instead of
        // passing silently.
        ct.ThrowIfCancellationRequested();
        ConsumedTokens.Add(purchaseToken);
        return Task.CompletedTask;
    }
}
