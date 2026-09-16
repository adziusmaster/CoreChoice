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
}
