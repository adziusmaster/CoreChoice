using CoreChoice.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Tests;

/// <summary>
/// A real SQLite database held in memory. Deliberately not the EF in-memory provider: the coin
/// ledger relies on raw conditional UPDATE statements, which the in-memory provider cannot run, so
/// testing against it would prove nothing about the code that actually protects the balance.
///
/// The connection is kept open for the factory's lifetime because an in-memory SQLite database is
/// destroyed when its last connection closes.
/// </summary>
internal static class InMemoryDb
{
    public static IDbContextFactory<ServerDbContext> Create()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(connection)
            .Options;

        return new SingleConnectionFactory(options);
    }

    private sealed class SingleConnectionFactory(DbContextOptions<ServerDbContext> options)
        : IDbContextFactory<ServerDbContext>
    {
        public ServerDbContext CreateDbContext() => new(options);
    }
}
