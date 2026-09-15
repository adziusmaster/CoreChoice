using CoreChoice.Server.Services;

namespace CoreChoice.Server.Endpoints;

internal static class CoinsEndpoint
{
    public static async Task<IResult> GetBalance(Guid deviceId, ICoinStore coins, CancellationToken ct)
    {
        // An unknown device is a new device, not an error: returning 404 would make the app's first
        // screen render an error state for every new install.
        var balance = await coins.GetBalanceAsync(deviceId, ct);
        return Results.Ok(new BalanceResponse(deviceId, balance));
    }

    public static async Task<IResult> Ensure(
        EnsureCoinsRequest request,
        HttpContext http,
        IGrantPolicy policy,
        IClientIpHasher hasher,
        CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty)
            return Results.BadRequest(new { error = "deviceId is required." });

        var ipHash = hasher.Hash(http.Connection.RemoteIpAddress);
        var balance = await policy.EnsureSeededAsync(request.DeviceId, ipHash, ct);

        return Results.Ok(new BalanceResponse(request.DeviceId, balance));
    }

    /// <summary>
    /// Grants the profile-completion bonus. Idempotent: a retry returns the unchanged balance with
    /// Granted=false rather than erroring or double-granting.
    ///
    /// Deliberately takes no balance into account and consults nothing about the person's coins
    /// before granting. Finishing the test is never gated on the ledger — that is the product's
    /// central promise, and this endpoint is where it would be easiest to break by accident.
    /// </summary>
    public static async Task<IResult> GrantProfileCompletion(
        ProfileGrantRequest request,
        HttpContext http,
        IGrantPolicy policy,
        IClientIpHasher hasher,
        CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty)
            return Results.BadRequest(new { error = "deviceId is required." });

        var ipHash = hasher.Hash(http.Connection.RemoteIpAddress);
        var outcome = await policy.TryGrantProfileCompletionAsync(request.DeviceId, ipHash, ct);

        return Results.Ok(new GrantResponse(
            request.DeviceId, outcome.Balance, outcome.Granted, outcome.Reason));
    }
}
