using CoreChoice.Server.Services;

namespace CoreChoice.Server.Endpoints;

/// <summary>
/// Public endpoint to redeem a promo code for analyses (coins), plus admin endpoints
/// (create/list/revoke) protected by a shared secret so the app owner can mint and manage codes.
/// </summary>
internal static class PromoEndpoint
{
    public const string SecretHeader = "X-Admin-Secret";
    private const int DefaultCoins = 10;

    public static async Task<IResult> Redeem(RedeemCodeRequest request, IPromoStore promos, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code))
            return Results.BadRequest(new { error = "deviceId and code are required." });

        var normalized = SqlitePromoStore.Normalize(request.Code);
        if (normalized.Length != SqlitePromoStore.CodeLength)
            return Results.BadRequest(new { error = $"A code must be {SqlitePromoStore.CodeLength} characters." });

        var result = await promos.RedeemAsync(request.DeviceId, normalized, ct);
        return result.Outcome switch
        {
            RedeemOutcome.Success => Results.Ok(new RedeemCodeResponse(result.CoinsGranted, result.Balance)),
            RedeemOutcome.NotFound => Results.NotFound(new { error = "invalid_code" }),
            RedeemOutcome.Revoked => Results.BadRequest(new { error = "revoked_code" }),
            RedeemOutcome.Expired => Results.BadRequest(new { error = "expired_code" }),
            RedeemOutcome.AlreadyRedeemed => Results.Conflict(new { error = "already_redeemed" }),
            _ => Results.BadRequest(new { error = "invalid_code" }),
        };
    }

    public static async Task<IResult> Create(CreatePromoRequest request, IPromoStore promos, CancellationToken ct)
    {
        var coins = request.Coins is { } c and > 0 ? c : DefaultCoins;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var normalized = SqlitePromoStore.Normalize(request.Code);
            if (normalized.Length != SqlitePromoStore.CodeLength)
                return Results.BadRequest(new { error = $"A custom code must be {SqlitePromoStore.CodeLength} characters." });
        }

        try
        {
            var promo = await promos.CreateAsync(request.Code, coins, request.ExpiresInDays, ct);
            return Results.Ok(ToResponse(promo, 0));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Results.Conflict(new { error = "A code with that value already exists." });
        }
    }

    public static async Task<IResult> List(IPromoStore promos, CancellationToken ct)
    {
        var all = await promos.ListAsync(ct);
        return Results.Ok(all.Select(s => new PromoResponse(
            s.Code, s.Coins, s.CreatedAt, s.ExpiresAt, s.Revoked, s.RedemptionCount)));
    }

    public static async Task<IResult> Revoke(string code, IPromoStore promos, CancellationToken ct)
    {
        var ok = await promos.RevokeAsync(code, ct);
        return ok ? Results.Ok(new { code = SqlitePromoStore.Normalize(code), revoked = true })
                  : Results.NotFound(new { error = "invalid_code" });
    }

    /// <summary>
    /// Gate for the admin promo routes. An unset (or blank) secret means the routes are not mapped
    /// at all (see Program.cs), so this filter only ever runs once a secret IS configured — at that
    /// point a wrong secret is answered with 401, not 404: the routes' existence is no longer a
    /// secret once the owner has one configured, so there is nothing left to hide by pretending
    /// otherwise, and 401 is what actually tells the caller their header was wrong.
    /// </summary>
    public static async ValueTask<object?> SecretFilter(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Admin:Secret"];

        if (string.IsNullOrWhiteSpace(configured))
            return Results.NotFound();

        var supplied = context.HttpContext.Request.Headers[SecretHeader].ToString();
        if (!CryptographicEquals(supplied, configured))
            return Results.Unauthorized();

        return await next(context);
    }

    private static PromoResponse ToResponse(Data.PromoCode c, int count) =>
        new(c.Code, c.Coins, c.CreatedAt, c.ExpiresAt, c.Revoked, count);

    // Fixed-time comparison: a secret checked with == leaks its length and prefix to a patient
    // caller. Hashing both sides to a fixed-width SHA-256 digest first — rather than padding and
    // truncating the raw strings — means every character of both inputs is significant and trailing
    // whitespace on either side no longer compares equal.
    private static bool CryptographicEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(a)),
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(b)));
}
