namespace CoreChoice.Application;

public sealed record CoinBalance(int Balance);

/// <summary>Outcome of a grant attempt. <paramref name="Balance"/> is always the current balance.</summary>
public sealed record GrantResult(bool Granted, int Balance, string? Reason);

/// <summary>
/// Every way <c>POST /api/promo/redeem</c> can answer. Deliberately not exceptions: a revoked or
/// already-redeemed code is an expected result the caller must tell apart to say something useful,
/// not a transport failure. Genuine transport failures (unreachable server, timeout, rate limiting)
/// still surface as the usual exceptions from <see cref="ICoinLedgerClient"/>'s other methods.
/// </summary>
public enum PromoRedemptionOutcome
{
    Redeemed,
    InvalidCode,
    RevokedCode,
    ExpiredCode,
    AlreadyRedeemed,

    /// <summary>The server rejected the code's shape itself (not 5 characters). The UI already
    /// gates submission on a 5-character code, so this is only reachable if that guard is ever
    /// bypassed or the server's rule changes; <see cref="Detail"/> carries the server's own
    /// sentence for that case since there is no fixed error code to map from.</summary>
    Malformed,
}

/// <summary><paramref name="CoinsGranted"/> and <paramref name="Balance"/> are only meaningful when
/// <paramref name="Outcome"/> is <see cref="PromoRedemptionOutcome.Redeemed"/>; both are zero
/// otherwise. <paramref name="Detail"/> carries the server's own message for
/// <see cref="PromoRedemptionOutcome.Malformed"/> and is null for every other outcome.</summary>
public sealed record PromoRedemptionResult(
    PromoRedemptionOutcome Outcome, int CoinsGranted, int Balance, string? Detail = null)
{
    public static PromoRedemptionResult Redeemed(int coinsGranted, int balance) =>
        new(PromoRedemptionOutcome.Redeemed, coinsGranted, balance);

    public static PromoRedemptionResult Failed(PromoRedemptionOutcome outcome, string? detail = null) =>
        new(outcome, CoinsGranted: 0, Balance: 0, detail);
}

/// <summary>The app's view of the coin ledger, which lives on the server.</summary>
public interface ICoinLedgerClient
{
    Task<CoinBalance> GetBalanceAsync(CancellationToken ct = default);

    /// <summary>First contact. Seeds the free coins; safe to call on every launch.</summary>
    Task<CoinBalance> EnsureSeededAsync(CancellationToken ct = default);

    /// <summary>
    /// Claims the coins granted for finishing the personality test. Idempotent on the server:
    /// a retry after a dropped connection returns the unchanged balance rather than granting twice
    /// or costing the person their grant.
    /// </summary>
    Task<GrantResult> ClaimProfileGrantAsync(CancellationToken ct = default);

    /// <summary>
    /// Redeems an un-consumed Play <see cref="PurchaseTicket"/> and grants the coins it paid for.
    /// Backed by <c>POST /api/billing/redeem</c>, which does not exist on the server yet — Play
    /// purchase validation is deliberately deferred to its own round of work, after internal
    /// testing. Calling this today fails like any other unreachable route, which is exactly what
    /// keeps <see cref="IBillingService.ConsumeAsync"/> from ever running for a real purchase: the
    /// caller must consume the ticket only after this call reports <see cref="GrantResult.Granted"/>,
    /// never before. The method exists now, fully wired, so switching billing on later is a matter
    /// of enabling the endpoint and the "buy" UI — not restructuring this call.
    /// </summary>
    Task<GrantResult> RedeemPurchaseAsync(PurchaseTicket ticket, CancellationToken ct = default);

    /// <summary>
    /// Redeems a promo code for this device via <c>POST /api/promo/redeem</c>. One code may be
    /// redeemed by many devices, but each device only once — a second attempt on the same device
    /// answers <see cref="PromoRedemptionOutcome.AlreadyRedeemed"/> rather than granting again. The
    /// endpoint is rate-limited (10/minute/IP) because short codes are guessable, so callers must
    /// not retry automatically on failure.
    /// </summary>
    Task<PromoRedemptionResult> RedeemPromoCodeAsync(string code, CancellationToken ct = default);
}
