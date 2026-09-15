using CoreChoice.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoreChoice.Server.Services;

internal sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// How long usage rows are kept. They exist for cost accounting and abuse triage, both of which
    /// are answered by recent data; keeping them forever turns an operational table into a permanent
    /// record of how often each device sought advice.
    /// </summary>
    public TimeSpan UsageLogTtl { get; set; } = TimeSpan.FromDays(90);
}

internal static class UsageLogRetention
{
    public static async Task<int> SweepAsync(
        IDbContextFactory<ServerDbContext> factory,
        TimeSpan ttl,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var cutoff = now - ttl;

        await using var db = await factory.CreateDbContextAsync(ct);
        // Translatable because At is persisted as UTC ticks; a DateTimeOffset comparison would
        // silently load every row and filter in memory.
        return await db.UsageLogs.Where(x => x.At <= cutoff).ExecuteDeleteAsync(ct);
    }
}

internal sealed class UsageLogRetentionService(
    IDbContextFactory<ServerDbContext> factory,
    IOptions<RetentionOptions> options,
    ILogger<UsageLogRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await UsageLogRetention.SweepAsync(
                    factory, options.Value.UsageLogTtl, DateTimeOffset.UtcNow, stoppingToken);

                if (deleted > 0)
                    logger.LogInformation("Usage log retention swept {Deleted} row(s).", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed sweep must never take the service down: the rows are operational, the
                // API is the product.
                logger.LogError(ex, "Usage log retention sweep failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
