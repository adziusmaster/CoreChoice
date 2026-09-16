using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CoreChoice.Server.Tests;

/// <summary>
/// Throws when a command inserts into "UsageLogs", simulating a logging failure (e.g. a full disk)
/// on the failure-path log write. Used to prove that a broken log write cannot downgrade the
/// specific 502/503 the endpoint already decided on to a generic 500.
///
/// Hooked on the reader-executing callback, not the non-query one: EF Core's SQLite provider runs
/// every INSERT through <c>ExecuteReaderAsync</c> (it always opens a reader, even for a
/// client-generated key with nothing to read back), so <see cref="DbCommandInterceptor"/>'s NonQuery
/// overrides are never invoked for an INSERT and would silently never fire.
/// </summary>
internal sealed class ThrowOnUsageLogInsertInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        if (command.CommandText.Contains("INSERT INTO \"UsageLogs\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Simulated disk failure writing the usage log.");

        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO \"UsageLogs\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Simulated disk failure writing the usage log.");

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
