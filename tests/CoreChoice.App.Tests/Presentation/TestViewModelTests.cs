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

    /// <summary>A real <see cref="IDbContextFactory{TContext}"/> over a file path, so closing it
    /// (and the connections it opened) truly discards nothing but what SQLite itself persisted.</summary>
    private sealed class FileBackedContextFactory(string dbPath) : IDbContextFactory<LocalDbContext>, IAsyncDisposable
    {
        private readonly DbContextOptions<LocalDbContext> _options =
            new DbContextOptionsBuilder<LocalDbContext>().UseSqlite($"Filename={dbPath}").Options;

        public LocalDbContext CreateDbContext() => new(_options);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
