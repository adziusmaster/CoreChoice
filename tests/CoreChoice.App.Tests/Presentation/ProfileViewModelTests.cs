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
        vm.Note.Should().NotBeEmpty();
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

    public static TheoryData<Exception> LedgerFailures =>
    [
        new HttpRequestException("offline"),
        new MalformedAdvisorResponseException("the response body was not valid JSON"),
        new DecisionUnavailableException("The request timed out."),
        new TimeoutException("the socket gave up"),
        new InvalidOperationException("something nobody predicted"),
    ];

    [Theory]
    [MemberData(nameof(LedgerFailures))]
    public async Task LoadAsync_WhateverTheLedgerThrows_ShouldStillRenderTheFreeResult(Exception failure)
    {
        // Arrange — the free-test promise must not depend on WHICH failure the ledger produces.
        // Every prior test here threw HttpRequestException alone, so narrowing the catch to that
        // one type stayed green: the invariant was asserted for a single exception shape, which is
        // how a too-narrow catch survives. This repo has already shipped three of those.
        var repository = new FakeProfileRepository();
        foreach (var item in IpipItemBank.Items)
            await repository.SaveAnswerAsync(item.Number, 3);

        var ledger = Substitute.For<ICoinLedgerClient>();
        ledger.ClaimProfileGrantAsync(default).ThrowsForAnyArgs(failure);
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Profile.IsPresent.Should().BeTrue();
        vm.Summary.Should().NotBeNullOrWhiteSpace();
        vm.GrantMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenTheCallerCancels_ShouldNotSwallowTheCancellation()
    {
        // Arrange — the inverse guard. The catch is filtered on !ct.IsCancellationRequested, so a
        // caller who navigates away gets a cancellation rather than a silently "successful" load.
        // Without this, "catch everything" would look identical to the correct implementation.
        var repository = new FakeProfileRepository();
        foreach (var item in IpipItemBank.Items)
            await repository.SaveAnswerAsync(item.Number, 3);

        var ledger = Substitute.For<ICoinLedgerClient>();
        using var cts = new CancellationTokenSource();
        ledger.ClaimProfileGrantAsync(default).ThrowsForAnyArgs(_ =>
        {
            cts.Cancel();
            return new OperationCanceledException(cts.Token);
        });
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        Func<Task> act = async () => await vm.LoadAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ===== RetakeAsync =====

    [Fact]
    public async Task RetakeAsync_ShouldClearTheStoredAnswers()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(AllFiftyAnswered());
        var ledger = Substitute.For<ICoinLedgerClient>();
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        await vm.RetakeAsync();

        // Assert
        (await repository.LoadAnswersAsync()).Responses.Should().BeEmpty();
    }

    [Fact]
    public async Task RetakeAsync_ShouldLeaveTheExistingProfileInPlace()
    {
        // Arrange — someone who abandons a retake before finishing the new fifty must not be left
        // with nothing: ClearAnswersAsync is specified to remove only the answers.
        var repository = new FakeProfileRepository();
        var existing = new OceanProfile(
            TraitScore.From(70), TraitScore.From(60), TraitScore.From(50),
            TraitScore.From(40), TraitScore.From(30));
        await repository.SaveProfileAsync(existing);
        repository.SeedAnswers(AllFiftyAnswered());
        var ledger = Substitute.For<ICoinLedgerClient>();
        var vm = new ProfileViewModel(repository, ledger);

        // Act
        await vm.RetakeAsync();

        // Assert
        (await repository.LoadProfileAsync()).Should().Be(existing);
        repository.SavedProfile.Should().Be(existing);
    }
}
