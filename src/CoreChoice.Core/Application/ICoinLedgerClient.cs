namespace CoreChoice.Application;

public sealed record CoinBalance(int Balance);

/// <summary>Outcome of a grant attempt. <paramref name="Balance"/> is always the current balance.</summary>
public sealed record GrantResult(bool Granted, int Balance, string? Reason);

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
}
