using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Data;

/// <summary>
/// Brings the database to the current shape, once, at boot.
///
/// Not EF migrations: this database holds real coin balances on a volume, and retrofitting a
/// migration history onto an unversioned production file is a worse risk than an explicit,
/// idempotent, additive script. EnsureCreated builds a fresh database; every change made AFTER the
/// first deploy must be added below as an explicit statement, because EnsureCreated will not alter
/// a table that already exists.
/// </summary>
internal static class SchemaInitializer
{
    public static async Task InitializeAsync(
        IDbContextFactory<ServerDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Creates every table for a brand-new database; a no-op where tables already exist.
        await db.Database.EnsureCreatedAsync(ct);

        // --- Post-first-deploy changes go below this line, each idempotent. ---

        // Promo codes, added after the first deploy. EnsureCreated above does nothing for a
        // database that already exists, so without these statements the live server starts
        // cleanly and then throws "no such table: PromoCodes" the first time anyone redeems —
        // which is exactly what happened on the deploy that introduced them.
        //
        // Column types match what the EF model produces: dates are UTC ticks (INTEGER), because
        // the SQLite provider cannot translate DateTimeOffset comparisons. The composite primary
        // key on PromoRedemptions IS the once-per-device rule — two concurrent redeems of one code
        // by one device collide here rather than both granting.
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PromoCodes" (
                "Code"      TEXT    NOT NULL CONSTRAINT "PK_PromoCodes" PRIMARY KEY,
                "Coins"     INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "ExpiresAt" INTEGER NULL,
                "Revoked"   INTEGER NOT NULL
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PromoRedemptions" (
                "Code"         TEXT NOT NULL,
                "DeviceId"     TEXT NOT NULL,
                "CoinsGranted" INTEGER NOT NULL,
                "RedeemedAt"   INTEGER NOT NULL,
                CONSTRAINT "PK_PromoRedemptions" PRIMARY KEY ("Code", "DeviceId")
            );
            """, ct);

        // Content the application cannot run without. Idempotent: does nothing when rows exist.
        await ContentSeed.SeedAsync(factory, ct);
    }
}
