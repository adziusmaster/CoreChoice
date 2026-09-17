using CoreChoice.Application;
using CoreChoice.Data;
using CoreChoice.Domain;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.App.Tests.Data;

public class DecisionHistoryTests
{
    private static DecisionAnalysis AnAnalysis() => new(
        "Keep the job. Build it on evenings and weekends.",
        68,
        ["The upside compounds", "The downside is bounded"],
        new OptionAssessment("Quit", ["Growth"], ["No income"]),
        new OptionAssessment("Stay", ["Stability"], ["Slower"]),
        "Your openness will make leaping look like adventure.",
        true);

    private static Dilemma ADilemma() =>
        Dilemma.Create("Quit my job", "Keep the job", "Eight months of savings.");

    [Fact]
    public async Task RecordAsync_ThenList_ShouldReturnEveryPartOfTheAnswer()
    {
        // Arrange — the nested lists are the interesting part: they are stored as JSON, so a
        // round trip is the only thing that proves the shape survives.
        var (history, conn) = Build();

        // Act
        await history.RecordAsync(ADilemma(), PersonaId.From("the-long-view"), DecisionWeight.From(4), AnAnalysis());
        var all = await history.ListAsync();

        // Assert
        all.Should().HaveCount(1);
        var d = all[0];
        d.Dilemma.OptionA.Should().Be("Quit my job");
        d.Dilemma.Context.Should().Be("Eight months of savings.");
        d.Persona.Value.Should().Be("the-long-view");
        d.Weight.Value.Should().Be(4);
        d.Analysis.Reasoning.Should().Equal("The upside compounds", "The downside is bounded");
        d.Analysis.OptionA.Strengths.Should().Equal("Growth");
        d.Analysis.OptionB.Risks.Should().Equal("Slower");
        d.Analysis.IsPersonalized.Should().BeTrue();
        conn.Dispose();
    }

    [Fact]
    public async Task ListAsync_ShouldReturnMostRecentFirst()
    {
        // Arrange
        var (history, conn) = Build();
        await history.RecordAsync(Dilemma.Create("First A", "First B", null), PersonaId.From("gut-check"), DecisionWeight.From(2), AnAnalysis());
        await Task.Delay(5);
        await history.RecordAsync(Dilemma.Create("Second A", "Second B", null), PersonaId.From("pure-logic"), DecisionWeight.From(4), AnAnalysis());

        // Act
        var all = await history.ListAsync();

        // Assert
        all.Select(d => d.Dilemma.OptionA).Should().Equal("Second A", "First A");
        conn.Dispose();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveOnlyThatEntry()
    {
        // Arrange — the record is the person's own; removing one must not disturb the rest.
        var (history, conn) = Build();
        var keep = await history.RecordAsync(Dilemma.Create("Keep A", "Keep B", null), PersonaId.From("gut-check"), DecisionWeight.From(3), AnAnalysis());
        var drop = await history.RecordAsync(Dilemma.Create("Drop A", "Drop B", null), PersonaId.From("gut-check"), DecisionWeight.From(3), AnAnalysis());

        // Act
        await history.DeleteAsync(drop);

        // Assert
        var all = await history.ListAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(keep);
        conn.Dispose();
    }

    [Fact]
    public async Task History_AfterEverythingIsTornDownAndRebuilt_ShouldStillBeThere()
    {
        // Arrange — history is promised to be kept indefinitely, so the thing worth proving is
        // that it survives the process, not merely the object. A real file, fully closed and
        // reopened, with pooling off so a pooled handle cannot make a deleted file look alive.
        var dbPath = Path.Combine(Path.GetTempPath(), $"corechoice-history-{Guid.NewGuid():N}.db");
        try
        {
            {
                await using var first = new FileFactory(dbPath);
                await using (var db = await ((IDbContextFactory<LocalDbContext>)first).CreateDbContextAsync())
                    await db.Database.EnsureCreatedAsync();
                var history = new SqliteDecisionHistory(first);
                await history.RecordAsync(ADilemma(), PersonaId.From("the-long-view"), DecisionWeight.From(4), AnAnalysis());
            }

            // Act
            await using var second = new FileFactory(dbPath);
            var reopened = await new SqliteDecisionHistory(second).ListAsync();

            // Assert
            reopened.Should().HaveCount(1);
            reopened[0].Analysis.Recommendation.Should().Be("Keep the job. Build it on evenings and weekends.");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static (IDecisionHistory History, SqliteConnection Conn) Build()
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(conn).Options;
        var factory = new MemoryFactory(options);
        using (var db = factory.CreateDbContext()) db.Database.EnsureCreated();
        return (new SqliteDecisionHistory(factory), conn);
    }

    private sealed class MemoryFactory(DbContextOptions<LocalDbContext> o) : IDbContextFactory<LocalDbContext>
    {
        public LocalDbContext CreateDbContext() => new(o);
    }

    private sealed class FileFactory(string path) : IDbContextFactory<LocalDbContext>, IAsyncDisposable
    {
        private readonly string _cs = $"Filename={path};Pooling=False";

        public LocalDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(_cs).Options);

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearPool(new SqliteConnection(_cs));
            return ValueTask.CompletedTask;
        }
    }
}
