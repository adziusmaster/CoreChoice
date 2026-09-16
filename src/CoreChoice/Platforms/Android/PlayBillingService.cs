using Android.BillingClient.Api;
using CoreChoice.Application;
using Microsoft.Maui.ApplicationModel;

namespace CoreChoice.Platforms.Android;

/// <summary>
/// Real Google Play Billing integration (Play Billing Library, via the
/// <c>Xamarin.Android.Google.BillingClient</c> binding). Sells the three consumable analysis
/// packs. Follows <c>PurePrep</c>'s own <c>PlayBillingService</c> connection, query and purchase
/// flow closely — this app just sells analyses instead of credits.
///
/// This file touches <c>Android.*</c> and <c>Microsoft.Maui.*</c> types throughout, so — like
/// <see cref="AndroidVoiceDictation"/> — it cannot be linked into <c>CoreChoice.App.Tests</c> at
/// all and cannot be exercised on this machine, which has no device. Every decision (which pack is
/// "best value", what the coins screen shows when the store cannot answer, the purchase order
/// itself) lives in <c>CoreChoice.Presentation.CoinsViewModel</c> instead, where it is covered by
/// tests; this class stays a thin adapter over the billing library on purpose.
///
/// Flow: connect ⇒ query each product ⇒ launch the purchase ⇒ receive the purchase via
/// <see cref="IPurchasesUpdatedListener"/>. The purchase is returned to the caller
/// <b>un-consumed</b>; <see cref="Presentation.CoinsViewModel.BuyAsync"/> redeems it with the
/// backend first and only then calls <see cref="ConsumeAsync"/>, so a failed grant never loses a
/// purchase — Google auto-refunds an un-consumed purchase after a few days.
/// </summary>
internal sealed class PlayBillingService : IBillingService
{
    public bool IsSupported => true;

    // Exactly the ids created in the Play Console (see IBillingService's own doc). Prices here are
    // placeholders only: FallbackPacks is what the UI shows when Play cannot be reached, never a
    // guess at what checkout would actually charge.
    public IReadOnlyList<AnalysisPack> FallbackPacks { get; } =
    [
        new AnalysisPack("corechoice.analyses.10", 10, "€2.99"),
        new AnalysisPack("corechoice.analyses.30", 30, "€5.99"),
        new AnalysisPack("corechoice.analyses.100", 100, "€14.99"),
    ];

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private BillingClient? _client;
    private TaskCompletionSource<PurchaseTicket?>? _purchaseTcs;

    public async Task<IReadOnlyList<AnalysisPack>> GetPacksAsync(CancellationToken ct = default)
    {
        try
        {
            var client = await EnsureConnectedAsync().ConfigureAwait(false);

            var resolved = new List<AnalysisPack>(FallbackPacks.Count);
            foreach (var pack in FallbackPacks)
            {
                var price = await TryGetFormattedPriceAsync(client, pack.ProductId).ConfigureAwait(false);
                // Keep the placeholder label for any pack Play could not price (e.g. not live yet).
                resolved.Add(price is null ? pack : pack with { DisplayPrice = price });
            }

            return resolved;
        }
        catch
        {
            // Offline or billing unavailable: fall back to the placeholder labels entirely, rather
            // than a partial mix of real and placeholder prices.
            return FallbackPacks;
        }
    }

    public async Task<PurchaseTicket?> BuyAsync(string productId, CancellationToken ct = default)
    {
        var client = await EnsureConnectedAsync().ConfigureAwait(false);

        // Reuse an earlier purchase that was never consumed (e.g. a prior grant failed): buying
        // again would be rejected by Google Play with ITEM_ALREADY_OWNED.
        var owned = await FindOwnedPurchaseAsync(client, productId).ConfigureAwait(false);
        if (owned is not null)
            return owned;

        var details = await GetProductDetailsAsync(client, productId).ConfigureAwait(false);

        var offerToken = details.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.OfferToken;

        var productParamsBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder()
            .SetProductDetails(details);
        if (!string.IsNullOrEmpty(offerToken))
            productParamsBuilder.SetOfferToken(offerToken);

        var flowParams = BillingFlowParams.NewBuilder()
            .SetProductDetailsParamsList(new List<BillingFlowParams.ProductDetailsParams>
            {
                productParamsBuilder.Build(),
            })
            .Build();

        var tcs = new TaskCompletionSource<PurchaseTicket?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _purchaseTcs = tcs;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity
                ?? throw new InvalidOperationException("No foreground activity is available to launch the purchase.");

            var launch = client.LaunchBillingFlow(activity, flowParams);
            if (launch.ResponseCode != BillingResponseCode.Ok)
            {
                _purchaseTcs = null;
                tcs.TrySetException(new InvalidOperationException(
                    $"Could not start the purchase ({launch.ResponseCode}): {launch.DebugMessage}"));
            }
        }).ConfigureAwait(false);

        using (ct.Register(() => tcs.TrySetResult(null)))
            return await tcs.Task.ConfigureAwait(false);
    }

    public async Task ConsumeAsync(string purchaseToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(purchaseToken))
            return;

        var client = await EnsureConnectedAsync().ConfigureAwait(false);
        var consumeParams = ConsumeParams.NewBuilder()
            .SetPurchaseToken(purchaseToken)
            .Build();

        await client.ConsumeAsync(consumeParams).ConfigureAwait(false);
    }

    private async Task<BillingClient> EnsureConnectedAsync()
    {
        if (_client is { IsReady: true } ready)
            return ready;

        await _connectGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_client is { IsReady: true } current)
                return current;

            _client?.EndConnection();

            var pending = PendingPurchasesParams.NewBuilder()
                .EnableOneTimeProducts()
                .Build();

            var client = BillingClient.NewBuilder(global::Android.App.Application.Context!)
                .SetListener(new PurchasesUpdatedListener(OnPurchasesUpdated))
                .EnablePendingPurchases(pending)
                .Build();

            var result = await client.StartConnectionAsync().ConfigureAwait(false);
            if (result.ResponseCode != BillingResponseCode.Ok)
                throw new InvalidOperationException(
                    $"Google Play Billing is unavailable ({result.ResponseCode}): {result.DebugMessage}");

            _client = client;
            return client;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    // Google Play's FormattedPrice is the localised, tax-inclusive string the user is actually
    // charged, so showing it guarantees the screen matches checkout in every country (VAT rates
    // vary by market).
    private static async Task<string?> TryGetFormattedPriceAsync(BillingClient client, string productId)
    {
        try
        {
            var details = await GetProductDetailsAsync(client, productId).ConfigureAwait(false);
            var formatted = details.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.FormattedPrice;
            return string.IsNullOrEmpty(formatted) ? null : formatted;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ProductDetails> GetProductDetailsAsync(BillingClient client, string productId)
    {
        var product = QueryProductDetailsParams.Product.NewBuilder()
            .SetProductId(productId)
            .SetProductType(BillingClient.ProductType.Inapp)
            .Build();

        var query = QueryProductDetailsParams.NewBuilder()
            .SetProductList(new List<QueryProductDetailsParams.Product> { product })
            .Build();

        var result = await client.QueryProductDetailsAsync(query).ConfigureAwait(false);

        // The binding exposes both the (Java) ProductDetailsList and a synthesized ProductDetails
        // list; read whichever is populated.
        var fetched = result.ProductDetailsList?.FirstOrDefault()
                      ?? result.ProductDetails?.FirstOrDefault();
        if (fetched is not null)
            return fetched;

        var reasons = new List<string>();
        if (result.Result is { } r)
            reasons.Add($"response {r.ResponseCode}: {r.DebugMessage}");
        if (result.UnfetchedProductList is { Count: > 0 } unfetched)
            reasons.Add("unfetched " + string.Join(", ",
                unfetched.Select(u => $"{u.ProductId} (status {u.StatusCodeValue})")));

        var detail = reasons.Count > 0 ? " — " + string.Join("; ", reasons) : string.Empty;
        throw new InvalidOperationException(
            $"Product '{productId}' is not available in Google Play yet{detail}. " +
            "It can take a little while after creating a product, and the app must be installed from a Play track.");
    }

    private static async Task<PurchaseTicket?> FindOwnedPurchaseAsync(BillingClient client, string productId)
    {
        var query = QueryPurchasesParams.NewBuilder()
            .SetProductType(BillingClient.ProductType.Inapp)
            .Build();

        var result = await client.QueryPurchasesAsync(query).ConfigureAwait(false);
        var purchase = result.Purchases?.FirstOrDefault(p =>
            p.PurchaseState == PurchaseState.Purchased &&
            p.Products is not null && p.Products.Contains(productId));

        return purchase is null ? null : new PurchaseTicket(productId, purchase.PurchaseToken!);
    }

    private void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
        var tcs = _purchaseTcs;
        if (tcs is null)
            return;
        _purchaseTcs = null;

        var code = result.ResponseCode;
        if (code == BillingResponseCode.UserCancelled)
        {
            tcs.TrySetResult(null);
            return;
        }

        if (code != BillingResponseCode.Ok || purchases is null)
        {
            tcs.TrySetException(new InvalidOperationException(
                $"The purchase did not complete ({code}): {result.DebugMessage}"));
            return;
        }

        var purchase = purchases.FirstOrDefault(p => p.PurchaseState == PurchaseState.Purchased);
        if (purchase is null)
        {
            // Purchase is still PENDING (e.g. cash / slow card). Nothing to grant yet.
            tcs.TrySetResult(null);
            return;
        }

        var productId = purchase.Products?.FirstOrDefault() ?? string.Empty;
        tcs.TrySetResult(new PurchaseTicket(productId, purchase.PurchaseToken!));
    }

    private sealed class PurchasesUpdatedListener(Action<BillingResult, IList<Purchase>?> callback)
        : Java.Lang.Object, IPurchasesUpdatedListener
    {
        public void OnPurchasesUpdated(BillingResult billingResult, IList<Purchase>? purchases) =>
            callback(billingResult, purchases);
    }
}
