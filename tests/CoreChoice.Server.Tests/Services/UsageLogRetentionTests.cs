using CoreChoice.Server.Data;
using CoreChoice.Server.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Tests.Services;

public class UsageLogRetentionTests
{
    private static async Task<IDbContextFactory<ServerDbContext>> BuildAsync(params DateTimeOffset[] timestamps)
    {
        var factory = InMemoryDb.Create();
        await SchemaInitializer.InitializeAsync(factory);

        await using var db = await factory.CreateDbContextAsync();
        foreach (var at in timestamps)
            db.UsageLogs.Add(new UsageLog { DeviceHash = "h", PersonaId = "pure-logic", At = at, Success = true });
        await db.SaveChangesAsync();

        return factory;
    }

    [Fact]
    public async Task SweepAsync_ShouldDeleteRowsOlderThanTheTtl()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var factory = await BuildAsync(now.AddDays(-120), now.AddDays(-91), now.AddDays(-10));

        // Act
        var deleted = await UsageLogRetention.SweepAsync(factory, TimeSpan.FromDays(90), now);

        // Assert
        deleted.Should().Be(2);
        await using var db = await factory.CreateDbContextAsync();
        (await db.UsageLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SweepAsync_WhenNothingIsOldEnough_ShouldDeleteNothing()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var factory = await BuildAsync(now.AddDays(-1), now.AddDays(-2));

        // Act
        var deleted = await UsageLogRetention.SweepAsync(factory, TimeSpan.FromDays(90), now);

        // Assert
        deleted.Should().Be(0);
    }

    [Fact]
    public async Task SweepAsync_ShouldIssueExactlyOneDeleteStatement()
    {
        // Arrange — pins the SQL *shape*, not just the row count: a client-side sweep that loads the
        // whole table and deletes row-by-row still returns correct counts, so a count-only assertion
        // cannot catch it losing its SQL translation. This interceptor can.
        var interceptor = new CommandRecordingInterceptor();
        var factory = InMemoryDb.Create(interceptor);
        await SchemaInitializer.InitializeAsync(factory);

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.UsageLogs.Add(new UsageLog
            {
                DeviceHash = "h", PersonaId = "pure-logic",
                At = DateTimeOffset.UtcNow.AddDays(-120), Success = true,
            });
            await db.SaveChangesAsync();
        }
        interceptor.Clear();

        // Act
        var deleted = await UsageLogRetention.SweepAsync(factory, TimeSpan.FromDays(90), DateTimeOffset.UtcNow);

        // Assert — exactly one command, a DELETE filtered on At, no unfiltered SELECT of the table.
        deleted.Should().Be(1);
        interceptor.Commands.Should().ContainSingle();
        var command = interceptor.Commands.Single();
        command.Should().Contain("DELETE");
        command.Should().Contain("At");
    }
}
