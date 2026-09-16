using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CoreChoice.Server.Tests;

/// <summary>
/// Cancels a caller-supplied <see cref="CancellationTokenSource"/> the moment the first INSERT
/// command has finished executing, then does nothing on every subsequent command. This is how the
/// grant-policy regression tests simulate "the caller's connection drops right after the marker row
/// commits, but before the credit runs": the marker row's SaveChangesAsync issues that INSERT as the
/// first write a grant call makes, so by the time control returns to the policy method, the token it
/// was given is already cancelled — exactly the window the fix in GrantPolicy.cs must survive.
///
/// Hooked on the reader-executed callbacks, not the non-query ones: EF Core's SQLite provider runs
/// every INSERT/UPDATE/DELETE through <c>ExecuteReaderAsync</c> (it always opens a reader, even for
/// a client-generated key with nothing to read back), so <see cref="DbCommandInterceptor"/>'s
/// NonQuery overrides are never invoked for these commands and would silently never fire.
/// </summary>
internal sealed class CancelAfterFirstWriteInterceptor(CancellationTokenSource cts) : DbCommandInterceptor
{
    private int _fired;

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal)
            && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            cts.Cancel();
        }

        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}
