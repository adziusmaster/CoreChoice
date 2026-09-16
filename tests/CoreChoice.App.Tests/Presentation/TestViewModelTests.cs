using Microsoft.Data.Sqlite;
using CoreChoice.Application;
using CoreChoice.Data;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.App.Tests.Presentation;

public class TestViewModelTests
{
    [Fact]
    public async Task LoadAsync_OnAFreshInstall_ShouldReturnTheFirstThreeItemsWithNullResponses()
    {
        // Arrange
        var vm = new TestViewModel(new FakeProfileRepository());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.CurrentPage.Should().HaveCount(3);
        vm.CurrentPage.Select(i => i.Number).Should().Equal(1, 2, 3);
        vm.CurrentPage.Should().OnlyContain(i => i.Response == null);
        vm.AnsweredCount.Should().Be(0);
    }

    [Fact]
    public async Task AnswerAsync_ShouldPersistTheAnswerThroughTheRepository()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        var vm = new TestViewModel(repository);
        await vm.LoadAsync();

        // Act
        await vm.AnswerAsync(1, 4);

        // Assert — the repository, not just the view model's in-memory state, holds the answer.
        var stored = await repository.LoadAnswersAsync();
        stored.Responses.Should().ContainKey(1).WhoseValue.Should().Be(4);
    }

    [Fact]
    public async Task LoadAsync_WhenSomeItemsAreAlreadyAnswered_ShouldSkipThemOnCurrentPage()
    {
        // Arrange — items 1 and 2 were answered in an earlier session.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers([new(1, 3), new(2, 5)]);
        var vm = new TestViewModel(repository);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.CurrentPage.Select(i => i.Number).Should().Equal(3, 4, 5);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportAnsweredCountFromStorageNotFromThisSession()
    {
        // Arrange — five answers already on disk before this view model ever ran.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(Enumerable.Range(1, 5).Select(n => new KeyValuePair<int, int>(n, 3)));
        var vm = new TestViewModel(repository);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.AnsweredCount.Should().Be(5);
    }

    [Fact]
    public async Task AdvanceAsync_ShouldReturnFalseUntilAllFiftyAreAnswered_AndTrueAtFifty()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        var vm = new TestViewModel(repository);
        await vm.LoadAsync();

        // Act / Assert — answer every item three at a time, checking AdvanceAsync after each trio.
        // Bounded deliberately. If the page ever stops advancing — the skip-answered filter
        // regressing is the realistic way — an unbounded loop here spins forever and the suite
        // reports a CI timeout instead of a failed assertion, which is no signal at all.
        var maxPages = IpipItemBank.ItemCount;   // far above the 17 pages fifty items can fill
        var results = new List<bool>();
        while (vm.CurrentPage.Count > 0)
        {
            foreach (var item in vm.CurrentPage.ToList())
                await vm.AnswerAsync(item.Number, 3);

            results.Add(await vm.AdvanceAsync());

            results.Count.Should().BeLessThan(maxPages,
                "the page must keep advancing; if it stops, fail here rather than hang");
        }

        results.Take(results.Count - 1).Should().OnlyContain(r => r == false);
        results[^1].Should().BeTrue();
        vm.AnsweredCount.Should().Be(IpipItemBank.ItemCount);
    }

    [Fact]
    public void Scoring_AnIncompleteSetOfResponses_ShouldThrowIncompleteProfileExceptionRatherThanScoreIt()
    {
        // Arrange — forty-nine of fifty answered. The view model relies on this guard: it never
        // attempts to interpret a partial set as a real, if smaller, profile.
        var responses = IpipItemBank.Items.Take(49).ToDictionary(i => i.Number, _ => 3);

        // Act
        var act = () => IpipScoring.Score(responses);

        // Assert
        act.Should().Throw<IncompleteProfileException>().Which.Answered.Should().Be(49);
    }

    [Fact]
    public async Task CanAdvance_WithThePageOnlyPartlyAnswered_ShouldBeFalse()
    {
        // Arrange — the "Next three" control binds to this. If it went true on the first tap, a
        // person could skip two items and reach the end with an unanswerable set.
        var vm = new TestViewModel(new FakeProfileRepository());
        await vm.LoadAsync();

        // Act — one of the three.
        await vm.AnswerAsync(vm.CurrentPage[0].Number, 3);

        // Assert
        vm.CanAdvance.Should().BeFalse();
    }

    [Fact]
    public async Task CanAdvance_WithEveryItemOnThePageAnswered_ShouldBeTrue()
    {
        // Arrange
        var vm = new TestViewModel(new FakeProfileRepository());
        await vm.LoadAsync();

        // Act
        foreach (var item in vm.CurrentPage.ToList())
            await vm.AnswerAsync(item.Number, 3);

        // Assert
        vm.CanAdvance.Should().BeTrue();
    }

    [Fact]
    public async Task CanAdvance_WithNoItemsLeft_ShouldBeFalse()
    {
        // Arrange — an empty page is the finished state, not an advanceable one.
        var repository = new FakeProfileRepository();
        foreach (var item in IpipItemBank.Items)
            await repository.SaveAnswerAsync(item.Number, 3);
        var vm = new TestViewModel(repository);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.CurrentPage.Should().BeEmpty();
        vm.CanAdvance.Should().BeFalse();
    }

    [Fact]
    public async Task Resume_AfterTwelveAnswersAndAFullRestart_ShouldLandOnItemThirteenWithThePreviousTwelveIntact()
    {
        // Arrange — a REAL file-backed SQLite database, not ":memory:": an in-memory connection
        // drops its schema the moment it is closed, so it cannot express "the app was force-stopped
        // and everything, including the connection, went away and came back." This is the single
        // behaviour that decides whether a low-conscientiousness user ever finishes the test.
        var dbPath = Path.Combine(Path.GetTempPath(), $"corechoice-resume-{Guid.NewGuid():N}.db");
        try
        {
            {
                await using var firstFactory = new FileBackedContextFactory(dbPath);
                IDbContextFactory<LocalDbContext> firstFactoryPort = firstFactory;
                await using (var db = await firstFactoryPort.CreateDbContextAsync())
                    await db.Database.EnsureCreatedAsync();

                var firstRepository = new SqliteProfileRepository(firstFactory);
                var firstViewModel = new TestViewModel(firstRepository);
                await firstViewModel.LoadAsync();

                for (var number = 1; number <= 12; number++)
                    await firstViewModel.AnswerAsync(number, 3);

                // Everything — the view model, the repository, and the context factory holding the
                // connection — is torn down here, mirroring a force-stopped app.
            }

            // Act — a brand-new view model, repository, and context factory over the same file.
            await using var secondFactory = new FileBackedContextFactory(dbPath);
            var secondRepository = new SqliteProfileRepository(secondFactory);
            var secondViewModel = new TestViewModel(secondRepository);
            await secondViewModel.LoadAsync();

            // Assert
            secondViewModel.AnsweredCount.Should().Be(12);
            secondViewModel.CurrentPage.Select(i => i.Number).Should().Equal(13, 14, 15);

            var stored = await secondRepository.LoadAnswersAsync();
            stored.Responses.Should().HaveCount(12);
            for (var number = 1; number <= 12; number++)
                stored.Responses.Should().ContainKey(number).WhoseValue.Should().Be(3);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Resume_WhenTheDatabaseFileIsDeletedBetweenRuns_ShouldNotStillReportTheAnswers()
    {
        // Arrange — a guard on the fixture, not on the app. The resume test above is only
        // meaningful if losing the file actually loses the data; with connection pooling left on it
        // did not, and the resume test passed with the file deleted. If this ever goes green again,
        // the resume test has stopped proving persistence.
        var dbPath = Path.Combine(Path.GetTempPath(), $"corechoice-resume-guard-{Guid.NewGuid():N}.db");
        try
        {
            {
                await using var firstFactory = new FileBackedContextFactory(dbPath);
                IDbContextFactory<LocalDbContext> port = firstFactory;
                await using (var db = await port.CreateDbContextAsync())
                    await db.Database.EnsureCreatedAsync();

                var repository = new SqliteProfileRepository(firstFactory);
                var viewModel = new TestViewModel(repository);
                await viewModel.LoadAsync();
                for (var number = 1; number <= 12; number++)
                    await viewModel.AnswerAsync(number, 3);
            }

            File.Delete(dbPath);

            // Act
            await using var secondFactory = new FileBackedContextFactory(dbPath);
            var secondViewModel = new TestViewModel(new SqliteProfileRepository(secondFactory));
            Func<Task> act = async () => await secondViewModel.LoadAsync();

            // Assert — either it throws because the schema is gone, or it comes back empty. What it
            // must never do is report the twelve answers that were deleted with the file.
            try
            {
                await act();
                secondViewModel.AnsweredCount.Should().Be(0,
                    "the answers went away with the file; reporting them means a pooled connection "
                    + "is still serving the old inode and the resume test is vacuous");
            }
            catch (SqliteException)
            {
                // The schema went with the file. Equally good evidence.
            }
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    /// <summary>
    /// A real <see cref="IDbContextFactory{TContext}"/> over a file path, with connection pooling
    /// OFF. Pooling defeats the whole point of this fixture: Microsoft.Data.Sqlite pools native
    /// connections by connection string, so a "restarted" factory silently receives a pooled handle
    /// still attached to the original inode. The test then passes even if the database file has
    /// been deleted outright — proving nothing about persistence, which is all it exists to prove.
    /// Disposing also clears the pool, so nothing leaks into the next test.
    /// </summary>
    private sealed class FileBackedContextFactory(string dbPath) : IDbContextFactory<LocalDbContext>, IAsyncDisposable
    {
        private readonly string _connectionString = $"Filename={dbPath};Pooling=False";

        private DbContextOptions<LocalDbContext> Options =>
            new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(_connectionString).Options;

        public LocalDbContext CreateDbContext() => new(Options);

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearPool(new SqliteConnection(_connectionString));
            return ValueTask.CompletedTask;
        }
    }
}
