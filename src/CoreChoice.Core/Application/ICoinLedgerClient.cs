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
}
