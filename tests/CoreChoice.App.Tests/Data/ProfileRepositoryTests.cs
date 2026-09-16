using CoreChoice.Application;
using CoreChoice.Data;
using CoreChoice.Domain;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.App.Tests.Data;

public class ProfileRepositoryTests
{
    private static (IProfileRepository Repo, SqliteConnection Conn) Build()
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(conn).Options;
        var factory = new TestFactory(options);
        using (var db = factory.CreateDbContext()) db.Database.EnsureCreated();
        return (new SqliteProfileRepository(factory), conn);
    }

    private sealed class TestFactory(DbContextOptions<LocalDbContext> o) : IDbContextFactory<LocalDbContext>
    {
        public LocalDbContext CreateDbContext() => new(o);
    }

    [Fact]
    public async Task SaveAnswerAsync_ThenLoad_ShouldReturnTheAnswer()
    {
        // Arrange
        var (repo, conn) = Build();

        // Act
        await repo.SaveAnswerAsync(17, 4);
        var answers = await repo.LoadAnswersAsync();

        // Assert
        answers.Responses.Should().ContainKey(17).WhoseValue.Should().Be(4);
        conn.Dispose();
    }

    [Fact]
    public async Task SaveAnswerAsync_WhenTheSameItemIsAnsweredTwice_ShouldKeepTheLatest()
    {
        // Arrange — people change their mind mid-test and tap a different number.
        var (repo, conn) = Build();
        await repo.SaveAnswerAsync(17, 4);

        // Act
        await repo.SaveAnswerAsync(17, 2);

        // Assert
        (await repo.LoadAnswersAsync()).Responses[17].Should().Be(2);
        conn.Dispose();
    }

    [Fact]
    public async Task LoadProfileAsync_BeforeTheTestIsScored_ShouldReturnNone()
    {
        // Arrange
        var (repo, conn) = Build();

        // Act
        var profile = await repo.LoadProfileAsync();

        // Assert — the absent case is a domain value, never a null for callers to remember.
        profile.IsPresent.Should().BeFalse();
        conn.Dispose();
    }

    [Fact]
    public async Task SaveProfileAsync_ThenLoad_ShouldRoundTripEveryTrait()
    {
        // Arrange
        var (repo, conn) = Build();
        var saved = new OceanProfile(
            TraitScore.From(88), TraitScore.From(45), TraitScore.From(62),
            TraitScore.From(70), TraitScore.From(58));

        // Act
        await repo.SaveProfileAsync(saved);
        var loaded = await repo.LoadProfileAsync();

        // Assert
        loaded.IsPresent.Should().BeTrue();
        loaded.Openness.Value.Should().Be(88);
        loaded.Neuroticism.Value.Should().Be(58);
        conn.Dispose();
    }

    [Fact]
    public async Task SaveProfileAsync_Twice_ShouldReplaceRatherThanAccumulate()
    {
        // Arrange — retaking the test must not leave two profiles on the phone.
        var (repo, conn) = Build();
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(10), TraitScore.From(10), TraitScore.From(10),
            TraitScore.From(10), TraitScore.From(10)));

        // Act
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(90), TraitScore.From(90), TraitScore.From(90),
            TraitScore.From(90), TraitScore.From(90)));

        // Assert
        (await repo.LoadProfileAsync()).Openness.Value.Should().Be(90);
        conn.Dispose();
    }

    [Fact]
    public async Task ClearAnswersAsync_ShouldRemoveEveryAnswerButLeaveTheProfile()
    {
        // Arrange — retaking clears answers; the old profile stays until the new one is scored,
        // so a person who abandons a retake is not left with nothing.
        var (repo, conn) = Build();
        await repo.SaveAnswerAsync(1, 3);
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(50), TraitScore.From(50), TraitScore.From(50),
            TraitScore.From(50), TraitScore.From(50)));

        // Act
        await repo.ClearAnswersAsync();

        // Assert
        (await repo.LoadAnswersAsync()).Responses.Should().BeEmpty();
        (await repo.LoadProfileAsync()).IsPresent.Should().BeTrue();
        conn.Dispose();
    }

    private static string ColumnType(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT typeof({column}) FROM {table} LIMIT 1";
        return (string)cmd.ExecuteScalar()!;
    }

    [Fact]
    public async Task SaveAnswerAsync_ShouldStoreTheDateAsAnIntegerOfUtcTicks()
    {
        // Arrange — asserting the STORED representation, not a round-trip. A round-trip passes
        // with or without the conversion, which is how a dropped HasConversion stayed invisible:
        // SQLite happily persists a DateTimeOffset as TEXT and reads it back intact. The column
        // being an integer is the thing that keeps date comparisons translatable server-side.
        var (repo, conn) = Build();

        // Act
        await repo.SaveAnswerAsync(17, 4);

        // Assert
        ColumnType(conn, "Answers", "AnsweredAt").Should().Be("integer");
        conn.Dispose();
    }

    [Fact]
    public async Task SaveProfileAsync_ShouldStoreTheDateAsAnIntegerOfUtcTicks()
    {
        // Arrange
        var (repo, conn) = Build();

        // Act
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(50), TraitScore.From(50), TraitScore.From(50),
            TraitScore.From(50), TraitScore.From(50)));

        // Assert
        ColumnType(conn, "Profiles", "ScoredAt").Should().Be("integer");
        conn.Dispose();
    }

    [Fact]
    public async Task SaveAnswerAsync_ForAnOffsetDate_ShouldStoreTheUtcInstantNotTheLocalClock()
    {
        // Arrange — UtcTicks, not Ticks. Storing the wall-clock reading of an offset date would
        // shift every timestamp by the offset, and the error only shows outside UTC+0.
        var (repo, conn) = Build();
        var before = DateTimeOffset.UtcNow.UtcTicks;

        // Act
        await repo.SaveAnswerAsync(17, 4);

        // Assert — the stored tick count is a UTC instant bracketing this test's own run.
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AnsweredAt FROM Answers LIMIT 1";
        var stored = (long)cmd.ExecuteScalar()!;
        stored.Should().BeInRange(before, DateTimeOffset.UtcNow.UtcTicks);
        conn.Dispose();
    }

    [Fact]
    public async Task LoadAnswersAsync_ShouldReportTheMostRecentAnswerTime()
    {
        // Arrange
        var (repo, conn) = Build();
        var before = DateTimeOffset.UtcNow;

        // Act
        await repo.SaveAnswerAsync(1, 3);
        await repo.SaveAnswerAsync(2, 5);
        var answers = await repo.LoadAnswersAsync();

        // Assert
        answers.UpdatedAt.Should().BeOnOrAfter(before);
        conn.Dispose();
    }

    [Fact]
    public async Task LoadAnswersAsync_WhenNothingIsAnswered_ShouldNotClaimAnUpdateTime()
    {
        // Arrange
        var (repo, conn) = Build();

        // Act
        var answers = await repo.LoadAnswersAsync();

        // Assert
        answers.Responses.Should().BeEmpty();
        answers.UpdatedAt.Should().Be(DateTimeOffset.MinValue);
        conn.Dispose();
    }
}
