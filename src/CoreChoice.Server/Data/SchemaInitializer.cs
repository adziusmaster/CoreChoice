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
        // (none yet)
    }
}
