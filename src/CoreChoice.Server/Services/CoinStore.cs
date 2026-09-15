using CoreChoice.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Services;

internal interface ICoinStore
{
    Task<int> GetBalanceAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>Seeds the device on first contact and returns the balance. Safe to call repeatedly.</summary>
    Task<int> EnsureDeviceAsync(Guid deviceId, int initialCoins, CancellationToken ct = default);

    /// <summary>Atomically deducts <paramref name="amount"/>. False when the balance cannot cover it.</summary>
    Task<bool> TrySpendAsync(Guid deviceId, int amount, CancellationToken ct = default);

    /// <summary>Returns coins after a paid operation failed.</summary>
    Task RefundAsync(Guid deviceId, int amount, CancellationToken ct = default);

    /// <summary>Adds coins, creating the row if needed. Returns the new balance.</summary>
    Task<int> GrantAsync(Guid deviceId, int amount, CancellationToken ct = default);
}

internal sealed class SqliteCoinStore(IDbContextFactory<ServerDbContext> factory) : ICoinStore
{
    public async Task<int> GetBalanceAsync(Guid deviceId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Coins.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        return row?.Balance ?? 0;
    }

    public async Task<int> EnsureDeviceAsync(Guid deviceId, int initialCoins, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Coins.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (existing is not null) return existing.Balance;

        var now = DateTimeOffset.UtcNow;
        db.Coins.Add(new DeviceCoins
        {
            DeviceId = deviceId,
            Balance = initialCoins,
            CreatedAt = now,
            UpdatedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return initialCoins;
        }
        catch (DbUpdateException)
        {
            // A concurrent first contact already seeded this device. Return what was persisted
            // rather than failing: both callers asked the same question and deserve the answer.
            return await GetBalanceAsync(deviceId, ct);
        }
    }

    public async Task<bool> TrySpendAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // One statement, evaluated by the database. A read-then-write here loses the double-tap
        // race, which is the realistic way a person spends a coin they do not have.
        var affected = await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Coins" SET "Balance" = "Balance" - {0}, "UpdatedAt" = {1}
            WHERE "DeviceId" = {2} AND "Balance" >= {0}
            """,
            [amount, DateTimeOffset.UtcNow, deviceId], ct);

        return affected > 0;
    }

    public async Task RefundAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Coins" SET "Balance" = "Balance" + {0}, "UpdatedAt" = {1}
            WHERE "DeviceId" = {2}
            """,
            [amount, DateTimeOffset.UtcNow, deviceId], ct);
    }

    public async Task<int> GrantAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var row = await db.Coins.FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (row is null)
        {
            row = new DeviceCoins { DeviceId = deviceId, Balance = amount, CreatedAt = now, UpdatedAt = now };
            db.Coins.Add(row);
        }
        else
        {
            row.Balance += amount;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return row.Balance;
    }
}
