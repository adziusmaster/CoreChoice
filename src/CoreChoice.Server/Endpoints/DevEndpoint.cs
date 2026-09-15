using CoreChoice.Server.Services;

namespace CoreChoice.Server.Endpoints;

internal sealed record DevGrantRequest(Guid DeviceId, int Amount);

internal static class DevEndpoint
{
    /// <summary>
    /// Gate for the development grant. Returns 404 rather than 401 when the secret is missing or
    /// wrong: an endpoint that says "wrong secret" has confirmed it exists, and this one hands out
    /// free coins.
    /// </summary>
    public static async ValueTask<object?> SecretFilter(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Dev:Secret"];

        if (string.IsNullOrWhiteSpace(configured))
            return Results.NotFound();

        var supplied = context.HttpContext.Request.Headers["X-Dev-Secret"].ToString();
        if (!CryptographicEquals(supplied, configured))
            return Results.NotFound();

        return await next(context);
    }

    public static async Task<IResult> Grant(DevGrantRequest request, ICoinStore coins, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || request.Amount <= 0)
            return Results.BadRequest(new { error = "deviceId and a positive amount are required." });

        var balance = await coins.GrantAsync(request.DeviceId, request.Amount, ct);
        return Results.Ok(new BalanceResponse(request.DeviceId, balance));
    }

    // Fixed-time comparison: a secret checked with == leaks its length and prefix to a patient caller.
    private static bool CryptographicEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a.PadRight(64)[..64]),
            System.Text.Encoding.UTF8.GetBytes(b.PadRight(64)[..64]));
}
