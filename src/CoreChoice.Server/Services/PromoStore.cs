using System.Security.Cryptography;
using CoreChoice.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Services;

internal enum RedeemOutcome
{
    Success,
    NotFound,
    Revoked,
    Expired,
    AlreadyRedeemed,
}

internal sealed record RedeemResult(RedeemOutcome Outcome, int CoinsGranted, int Balance);

internal sealed record PromoSummary(
    string Code,
    int Coins,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Revoked,
    int RedemptionCount);

internal interface IPromoStore
{
    Task<RedeemResult> RedeemAsync(Guid deviceId, string code, CancellationToken ct = default);
    Task<PromoCode> CreateAsync(string? code, int coins, int? expiresInDays, CancellationToken ct = default);
    Task<IReadOnlyList<PromoSummary>> ListAsync(CancellationToken ct = default);
    Task<bool> RevokeAsync(string code, CancellationToken ct = default);
}

internal sealed class SqlitePromoStore(IDbContextFactory<ServerDbContext> factory, ICoinStore coins) : IPromoStore
{
    // Unambiguous alphabet: no 0/O, 1/I, so codes are easy to read out loud and type.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 5;

    public static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public async Task<RedeemResult> RedeemAsync(Guid deviceId, string code, CancellationToken ct = default)
    {
        var normalized = Normalize(code);
        await using var db = await factory.CreateDbContextAsync(ct);

        var promo = await db.PromoCodes.AsNoTracking().FirstOrDefaultAsync(x => x.Code == normalized, ct);
        if (promo is null)
            return new RedeemResult(RedeemOutcome.NotFound, 0, await coins.GetBalanceAsync(deviceId, ct));
        if (promo.Revoked)
            return new RedeemResult(RedeemOutcome.Revoked, 0, await coins.GetBalanceAsync(deviceId, ct));
        if (promo.ExpiresAt is { } exp && exp <= DateTimeOffset.UtcNow)
            return new RedeemResult(RedeemOutcome.Expired, 0, await coins.GetBalanceAsync(deviceId, ct));

        // Record the redemption first; the composite PK (see ServerDbContext) makes a double-redeem
        // a duplicate-key error rather than a race a prior read could miss.
        db.PromoRedemptions.Add(new PromoRedemption
        {
            Code = normalized,
            DeviceId = deviceId,
            CoinsGranted = promo.Coins,
            RedeemedAt = DateTimeOffset.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // This device already redeemed this code (or lost the race to redeem it right now).
            return new RedeemResult(RedeemOutcome.AlreadyRedeemed, 0, await coins.GetBalanceAsync(deviceId, ct));
        }

        // The PromoRedemption marker row above is already committed at this point: if the coin
        // grant below were cancelled by the caller's token, the device would be left recorded as
        // having redeemed the code while holding none of the coins it promised, and every retry
        // would find the row and return AlreadyRedeemed forever — the grant and the record would
        // have diverged. CancellationToken.None guarantees the grant this row promises actually
        // lands regardless of what the caller's connection is doing, matching the fix GrantPolicy
        // applies to its own marker rows (DeviceSeed / ProfileGrant).
        var balance = await coins.GrantAsync(deviceId, promo.Coins, CancellationToken.None);
        return new RedeemResult(RedeemOutcome.Success, promo.Coins, balance);
    }

    public async Task<PromoCode> CreateAsync(
        string? code, int coins, int? expiresInDays, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var normalized = string.IsNullOrWhiteSpace(code) ? await GenerateUniqueAsync(db, ct) : Normalize(code);
        var promo = new PromoCode
        {
            Code = normalized,
            Coins = coins,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresInDays is { } days and > 0 ? DateTimeOffset.UtcNow.AddDays(days) : null,
            Revoked = false,
        };
        db.PromoCodes.Add(promo);
        await db.SaveChangesAsync(ct);
        return promo;
    }

    public async Task<IReadOnlyList<PromoSummary>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var codes = await db.PromoCodes.AsNoTracking().ToListAsync(ct);
        var counts = await db.PromoRedemptions.AsNoTracking()
            .GroupBy(x => x.Code)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);

        return codes
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PromoSummary(
                c.Code, c.Coins, c.CreatedAt, c.ExpiresAt, c.Revoked,
                counts.TryGetValue(c.Code, out var n) ? n : 0))
            .ToList();
    }

    public async Task<bool> RevokeAsync(string code, CancellationToken ct = default)
    {
        var normalized = Normalize(code);
        await using var db = await factory.CreateDbContextAsync(ct);
        var promo = await db.PromoCodes.FirstOrDefaultAsync(x => x.Code == normalized, ct);
        if (promo is null) return false;
        promo.Revoked = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<string> GenerateUniqueAsync(ServerDbContext db, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = RandomCode();
            if (!await db.PromoCodes.AnyAsync(x => x.Code == candidate, ct))
                return candidate;
        }
        throw new InvalidOperationException("Could not generate a unique promo code.");
    }

    private static string RandomCode()
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
