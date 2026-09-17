using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;

namespace CoreChoice.Presentation;

/// <summary>
/// Backs the analyses/coins screen (artboard 8 · "Analyses"). No MAUI type appears anywhere in
/// this file, same discipline as <see cref="DilemmaViewModel"/> and <see cref="AnalysisViewModel"/>:
/// <c>CoreChoice.App.Tests</c> links this in by source, and anything here that touched
/// <c>Microsoft.Maui.*</c> would silently drop out of test coverage entirely.
///
/// <para><b>Prices always come from Google Play</b> when the store can be reached:
/// <see cref="IBillingService.GetPacksAsync"/> reads the localised, tax-inclusive
/// <c>FormattedPrice</c> Google charges at checkout — the only figure that is correct in every
/// country, since VAT rates differ. <see cref="PricesAreFromStore"/> is false only when the store
/// could not be reached (offline, billing unavailable, or the products are not yet live in the
/// Play Console — the case today), in which case <see cref="Packs"/> falls back to
/// <see cref="IBillingService.FallbackPacks"/>'s placeholder labels. That flag drives the page's
/// own "these are placeholder prices" notice — a stale price shown as though it were real is the
/// one thing that turns a pricing bug into a complaint.</para>
///
/// <para><b>Buying is disabled for now.</b> There is no backend endpoint yet to redeem a Play
/// purchase (<see cref="ICoinLedgerClient.RedeemPurchaseAsync"/> calls
/// <c>POST /api/billing/redeem</c>, which does not exist on the server — Play validation is its
/// own round of work, after internal testing). <see cref="CanBuy"/> is gated by the
/// <see cref="PurchasingEnabled"/> constant below, hardcoded false, so the page's buy buttons stay
/// disabled regardless of whether the device itself supports billing. <see cref="BuyAsync"/> is
/// still fully implemented and tested below, so switching billing on later is exactly one line:
/// flip that constant once the endpoint ships.</para>
///
/// <para><b>The purchase order is not negotiable</b> (see <see cref="BuyAsync"/>): the ticket
/// <see cref="IBillingService.BuyAsync"/> returns is redeemed with the backend FIRST, and only
/// <see cref="IBillingService.ConsumeAsync"/> once the redemption call actually reports a grant.
/// Consuming first would mean a person pays and gets nothing if the network drops in between — and
/// the purchase would be gone for good, since a consumed product cannot be re-redeemed.</para>
/// </summary>
public sealed partial class CoinsViewModel(IBillingService billing, ICoinLedgerClient ledger) : ObservableObject
{
    /// <summary>
    /// Flip to true once <c>POST /api/billing/redeem</c> exists on the server. Kept as the single
    /// gate on <see cref="CanBuy"/> so enabling purchasing later needs no other change here.
    /// </summary>
    private const bool PurchasingEnabled = false;

    // The three product ids created in the Play Console. Sourced from AnalysisPackCatalog — the
    // single place this app lists them — rather than repeated here, so this list and
    // PlayBillingService's FallbackPacks cannot drift apart (see AnalysisPackCatalog's own doc).
    public const string TenAnalysesProductId = AnalysisPackCatalog.TenAnalysesProductId;
    public const string ThirtyAnalysesProductId = AnalysisPackCatalog.ThirtyAnalysesProductId;
    public const string HundredAnalysesProductId = AnalysisPackCatalog.HundredAnalysesProductId;

    [ObservableProperty]
    private int balance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuy))]
    [NotifyPropertyChangedFor(nameof(TenAnalysesCount))]
    [NotifyPropertyChangedFor(nameof(TenDisplayPrice))]
    [NotifyPropertyChangedFor(nameof(ThirtyAnalysesCount))]
    [NotifyPropertyChangedFor(nameof(ThirtyDisplayPrice))]
    [NotifyPropertyChangedFor(nameof(HundredAnalysesCount))]
    [NotifyPropertyChangedFor(nameof(HundredDisplayPrice))]
    private IReadOnlyList<AnalysisPack> packs = [];

    /// <summary>
    /// Each pack's count and price, found by its known product id and exposed as flat,
    /// non-nullable properties rather than the <see cref="AnalysisPack"/> record itself — the page
    /// binds three fixed cards directly (this screen's three offers are visually distinct, the
    /// middle one badged "Best value", not repetitions of one template), and a nested
    /// <c>Pack.Property</c> binding path through a nullable record tripped up the XAML compiled-
    /// binding source generator (a chained-nullable getter it could not emit). The count falls back
    /// to what the product id itself means (10/30/100) before <see cref="LoadAsync"/> has run; the
    /// price is blank until then, since there is no honest placeholder for a price at that point.
    /// </summary>
    public int TenAnalysesCount => FindPack(TenAnalysesProductId)?.Analyses ?? 10;
    public string TenDisplayPrice => FindPack(TenAnalysesProductId)?.DisplayPrice ?? string.Empty;
    public int ThirtyAnalysesCount => FindPack(ThirtyAnalysesProductId)?.Analyses ?? 30;
    public string ThirtyDisplayPrice => FindPack(ThirtyAnalysesProductId)?.DisplayPrice ?? string.Empty;
    public int HundredAnalysesCount => FindPack(HundredAnalysesProductId)?.Analyses ?? 100;
    public string HundredDisplayPrice => FindPack(HundredAnalysesProductId)?.DisplayPrice ?? string.Empty;

    private AnalysisPack? FindPack(string productId) =>
        Packs.FirstOrDefault(p => p.ProductId == productId);

    /// <summary>
    /// Whether <see cref="Packs"/> holds Google Play's own prices (true) or the placeholder labels
    /// from <see cref="IBillingService.FallbackPacks"/> (false). The page must show this honestly
    /// rather than let a placeholder pass for a real price.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlaceholderPrices))]
    private bool pricesAreFromStore;

    /// <summary>The page's placeholder-prices notice binds to this rather than negating
    /// <see cref="PricesAreFromStore"/> itself in XAML.</summary>
    public bool ShowsPlaceholderPrices => !PricesAreFromStore;

    /// <summary>
    /// Whether the buy buttons should be enabled at all. False whenever the device itself cannot
    /// bill (<see cref="IBillingService.IsSupported"/>), and — for now, unconditionally — while
    /// <see cref="PurchasingEnabled"/> stays false, because there is nowhere yet to redeem a
    /// purchase. The app is fully usable on the free analyses in the meantime.
    /// </summary>
    public bool CanBuy => billing.IsSupported && PurchasingEnabled;

    /// <summary>A message for the page to show after a buy attempt: null on success or on a plain
    /// cancellation (nothing went wrong, so nothing to say), set otherwise.</summary>
    [ObservableProperty]
    private string? message;

    /// <summary>The code as typed. Not case-normalised here — the server normalises, and showing
    /// back exactly what was typed avoids a distracting case-flip mid-entry.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRedeem))]
    private string promoCode = string.Empty;

    /// <summary>True while a redemption call is in flight. Folded into <see cref="CanRedeem"/>
    /// rather than kept separate: the endpoint is rate-limited (10/minute/IP) precisely because
    /// short codes are guessable, so the button must go inert for the duration of the call rather
    /// than let a second tap queue up another request.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRedeem))]
    private bool isRedeeming;

    /// <summary>Result of the last redemption attempt, in plain language — set for every outcome,
    /// success included, since "10 analyses added" is exactly as much this page's job to say as
    /// "that code has already been used on this device".</summary>
    [ObservableProperty]
    private string? redeemMessage;

    /// <summary>Mirrors <see cref="DilemmaViewModel.CanSubmit"/>'s gating: the button stays inert
    /// until the field holds a plausible code, so a half-typed code can never be submitted (and,
    /// combined with <see cref="IsRedeeming"/>, so a second tap cannot queue up while one is still
    /// in flight against a rate-limited endpoint).</summary>
    public bool CanRedeem => !IsRedeeming && PromoCode.Trim().Length == PromoCodeLength;

    private const int PromoCodeLength = 5;

    /// <summary>
    /// Loads the current balance and the packs available to buy. The two are independent: a
    /// failure fetching one never blocks the other, since either can fail for its own unrelated
    /// reason (the ledger being unreachable says nothing about whether Play billing works, and
    /// vice versa).
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var current = await ledger.GetBalanceAsync(ct);
            Balance = current.Balance;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // The balance simply stays whatever it last was; this screen still has packs to show.
        }

        try
        {
            Packs = await billing.GetPacksAsync(ct);
            PricesAreFromStore = true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Offline, billing unavailable, or the products are not live in Play yet — all the
            // same to the person: show the placeholder labels and say plainly that is what they are.
            Packs = billing.FallbackPacks;
            PricesAreFromStore = false;
        }
    }

    /// <summary>
    /// Runs one purchase to completion, in the one order that is safe:
    /// <list type="number">
    /// <item><see cref="IBillingService.BuyAsync"/> runs the Play flow and returns an
    /// <b>un-consumed</b> ticket, or null if the person cancelled.</item>
    /// <item>The backend redeems the ticket and grants the coins
    /// (<see cref="ICoinLedgerClient.RedeemPurchaseAsync"/>).</item>
    /// <item>Only once that call reports <see cref="GrantResult.Granted"/> is the ticket consumed
    /// (<see cref="IBillingService.ConsumeAsync"/>), releasing the product for repurchase.</item>
    /// </list>
    /// If redemption fails or is refused, the ticket is deliberately left un-consumed: Google
    /// either refunds an un-consumed purchase automatically after a few days, or it can still be
    /// redeemed for real later — consuming it first would throw both of those away for nothing.
    /// A cancelled purchase (a null ticket) is not an error: nothing happened on either side, so
    /// nothing is shown.
    /// </summary>
    public async Task BuyAsync(string productId, CancellationToken ct = default)
    {
        Message = null;

        PurchaseTicket? ticket;
        try
        {
            ticket = await billing.BuyAsync(productId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            Message = "The purchase could not be started.";
            return;
        }

        if (ticket is null)
            return;

        GrantResult grant;
        try
        {
            grant = await ledger.RedeemPurchaseAsync(ticket, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            Message = "That could not be completed. If you were charged, it will be refunded automatically.";
            return;
        }

        if (!grant.Granted)
        {
            Message = grant.Reason ?? "That could not be completed.";
            return;
        }

        Balance = grant.Balance;

        // Only now, with the coins actually on the ledger, is it safe to let Google resell this
        // product to the same person — and that finalisation must complete regardless of what the
        // caller's own token does next: a cancellation landing in this exact window must not leave
        // the person holding granted coins on an un-consumed purchase that then blocks a future
        // repurchase with ITEM_ALREADY_OWNED. CancellationToken.None on purpose, same rule this
        // codebase already applies to rollbacks: the action that finishes a transaction is never
        // cancellable by whatever is racing against it.
        await billing.ConsumeAsync(ticket.PurchaseToken, CancellationToken.None);
    }

    /// <summary>
    /// Redeems <see cref="PromoCode"/> against the ledger. Never retries on failure — the endpoint
    /// is rate-limited (10/minute/IP) precisely because short codes are guessable, so a retry loop
    /// here would risk locking the person out entirely; a genuine failure is reported once and left
    /// for the person to act on. Deliberately does not require <see cref="CanRedeem"/> to already be
    /// true: the page's button is gated on it, but this guards the same way regardless of caller.
    /// </summary>
    public async Task RedeemPromoCodeAsync(CancellationToken ct = default)
    {
        if (!CanRedeem)
            return;

        var code = PromoCode.Trim();
        RedeemMessage = null;
        IsRedeeming = true;
        try
        {
            PromoRedemptionResult result;
            try
            {
                result = await ledger.RedeemPromoCodeAsync(code, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                RedeemMessage = "That could not be checked right now. Please try again in a moment.";
                return;
            }

            RedeemMessage = DescribeRedemption(result);
            if (result.Outcome == PromoRedemptionOutcome.Redeemed)
            {
                Balance = result.Balance;
                PromoCode = string.Empty;
            }
        }
        finally
        {
            IsRedeeming = false;
        }
    }

    private static string DescribeRedemption(PromoRedemptionResult result) => result.Outcome switch
    {
        PromoRedemptionOutcome.Redeemed => result.CoinsGranted == 1
            ? $"Added 1 analysis. Balance is now {result.Balance}."
            : $"Added {result.CoinsGranted} analyses. Balance is now {result.Balance}.",
        PromoRedemptionOutcome.InvalidCode => "That code was not recognised.",
        PromoRedemptionOutcome.RevokedCode => "That code has been revoked and can no longer be used.",
        PromoRedemptionOutcome.ExpiredCode => "That code has expired.",
        PromoRedemptionOutcome.AlreadyRedeemed => "That code has already been used on this device.",
        PromoRedemptionOutcome.Malformed => result.Detail ?? "That code is not valid.",
        _ => "That code could not be redeemed.",
    };
}
