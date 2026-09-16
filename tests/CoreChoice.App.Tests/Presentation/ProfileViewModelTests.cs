using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.App.Tests.Presentation;

public class ProfileViewModelTests
{
    private static Dictionary<int, int> AllFiftyAnswered() =>
        IpipItemBank.Items.ToDictionary(i => i.Number, _ => 3);

    [Fact]
    public async Task LoadAsync_WhenNoProfileIsSavedYet_ShouldScoreFromAnswersAndSaveIt()
    {
        // Arrange — happy path: the test just finished, nothing scored yet.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(AllFiftyAnswered());
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.ClaimProfileGrantAsync(default).ReturnsForAnyArgs(new GrantResult(true, 50, null));
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Profile.IsPresent.Should().BeTrue();
        vm.Summary.Should().NotBeEmpty();
        repository.SaveProfileCallCount.Should().Be(1);
        repository.SavedProfile.Should().Be(vm.Profile);
    }

    [Fact]
    public async Task LoadAsync_WhenAProfileIsAlreadySaved_ShouldNotRescoreOrResave()
    {
        // Arrange — a retake-free load: the profile already exists on disk.
        var repository = new FakeProfileRepository();
        var existing = new OceanProfile(
            TraitScore.From(70), TraitScore.From(60), TraitScore.From(50),
            TraitScore.From(40), TraitScore.From(30));
        await repository.SaveProfileAsync(existing);
        var saveCallsBefore = repository.SaveProfileCallCount;
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.ClaimProfileGrantAsync(default).ReturnsForAnyArgs(new GrantResult(false, 50, "already-granted"));
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Profile.Should().Be(existing);
        repository.SaveProfileCallCount.Should().Be(saveCallsBefore);
    }

    [Fact]
    public async Task LoadAsync_WhenAnswersAreIncomplete_ShouldThrowIncompleteProfileExceptionRatherThanScorePartially()
    {
        // Arrange — this screen should only ever be reached at fifty of fifty; if it is not, the
        // failure must be loud rather than a plausible-looking, wrong profile.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(IpipItemBank.Items.Take(30).ToDictionary(i => i.Number, _ => 3));
        var ledger = Substitute.For<ICoinLedgerClient>();
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        Func<Task> act = async () => await vm.LoadAsync();

        // Assert
        await act.Should().ThrowAsync<IncompleteProfileException>();
        repository.SaveProfileCallCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_WhenTheGrantCallThrows_ShouldStillRenderTheProfileWithNoGrantMessage()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(AllFiftyAnswered());
        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.ClaimProfileGrantAsync(default).ThrowsForAnyArgs(new HttpRequestException("offline"));
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Profile.IsPresent.Should().BeTrue();
        vm.GrantMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WithALedgerThatThrowsOnEveryCall_ShouldStillScoreSaveAndExposeTheFreeResult()
    {
        // Arrange — the free-test invariant, proven with a stub that throws on every ledger
        // method (not just the one this view model happens to call today), so a future call added
        // here cannot silently reintroduce a paywall on a ten-minute result.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(AllFiftyAnswered());
        var vm = new ProfileViewModel(repository, new AlwaysThrowingCoinLedgerClient());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Profile.IsPresent.Should().BeTrue();
        repository.SaveProfileCallCount.Should().Be(1);
        vm.GrantMessage.Should().BeNull();
    }
}
