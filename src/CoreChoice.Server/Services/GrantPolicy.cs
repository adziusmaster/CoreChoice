using CoreChoice.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoreChoice.Server.Services;

/// <summary>Result of a grant attempt. <paramref name="Balance"/> is always the current balance.</summary>
internal sealed record GrantOutcome(bool Granted, int Balance, string? Reason);

internal interface IGrantPolicy
{
    /// <summary>Seeds first-contact coins subject to the origin cap. Returns the balance.</summary>
    Task<int> EnsureSeededAsync(Guid deviceId, string? ipHash, CancellationToken ct = default);

    /// <summary>Grants the profile-completion bonus once per device, subject to the origin cap.</summary>
    Task<GrantOutcome> TryGrantProfileCompletionAsync(Guid deviceId, string? ipHash, CancellationToken ct = default);
}

internal sealed class SqliteGrantPolicy(
    IDbContextFactory<ServerDbContext> factory,
    ICoinStore coins,
    IOptions<CoinOptions> options) : IGrantPolicy
{
    private readonly CoinOptions _options = options.Value;

    public async Task<int> EnsureSeededAsync(Guid deviceId, string? ipHash, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var alreadySeeded = await db.DeviceSeeds.AsNoTracking()
            .AnyAsync(x => x.DeviceId == deviceId, ct);
        if (alreadySeeded)
            return await coins.GetBalanceAsync(deviceId, ct);

        var capped = await IsOriginCappedAsync(db, ipHash, ct);
        var amount = capped ? 0 : _options.FirstContactGrant;

        db.DeviceSeeds.Add(new DeviceSeed
        {
            DeviceId = deviceId,
            IpHash = ipHash,
            SeededAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent first contact for the same device; the other caller seeded it.
            return await coins.GetBalanceAsync(deviceId, ct);
        }

        // The DeviceSeed marker row above is already committed at this point: if this credit is
        // cancelled by the same token, the device is left recorded as seeded while holding zero
        // coins, and every retry short-circuits on the `alreadySeeded` check above and returns the
        // unchanged (zero) balance forever. CancellationToken.None guarantees the credit that the
        // marker row promises actually lands, regardless of what the caller's connection is doing.
        return await coins.EnsureDeviceAsync(deviceId, amount, CancellationToken.None);
    }

    public async Task<GrantOutcome> TryGrantProfileCompletionAsync(
        Guid deviceId, string? ipHash, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var already = await db.ProfileGrants.AsNoTracking()
            .AnyAsync(x => x.DeviceId == deviceId, ct);
        if (already)
            return new GrantOutcome(false, await coins.GetBalanceAsync(deviceId, ct), "already-granted");

        if (await IsOriginCappedAsync(db, ipHash, ct))
            return new GrantOutcome(false, await coins.GetBalanceAsync(deviceId, ct), "origin-cap");

        db.ProfileGrants.Add(new ProfileGrant
        {
            DeviceId = deviceId,
            IpHash = ipHash,
            CoinsGranted = _options.ProfileCompletionGrant,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two claims raced; the primary key on DeviceId means exactly one row was written.
            return new GrantOutcome(false, await coins.GetBalanceAsync(deviceId, ct), "already-granted");
        }

        // Same shape as EnsureSeededAsync above: the ProfileGrant marker row is already committed,
        // so a cancellation here must not be allowed to strand the device as "already granted" with
        // nothing to show for it. CancellationToken.None ensures the credit this row promises is
        // applied even if the caller has already disconnected.
        var balance = await coins.GrantAsync(deviceId, _options.ProfileCompletionGrant, CancellationToken.None);
        return new GrantOutcome(true, balance, null);
    }

    /// <summary>
    /// Counts free grants of both kinds from one origin inside the window. Unknown origins are never
    /// capped: a missing forwarded header must not lock out a legitimate person.
    /// </summary>
    private async Task<bool> IsOriginCappedAsync(ServerDbContext db, string? ipHash, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ipHash)) return false;

        var since = DateTimeOffset.UtcNow - _options.OriginWindow;

        var seeds = await db.DeviceSeeds.AsNoTracking()
            .CountAsync(x => x.IpHash == ipHash && x.SeededAt >= since, ct);
        var grants = await db.ProfileGrants.AsNoTracking()
            .CountAsync(x => x.IpHash == ipHash && x.GrantedAt >= since, ct);

        return seeds + grants >= _options.MaxGrantsPerOrigin;
    }
}
