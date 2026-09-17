using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class TestIntroViewModelTests
{
    [Fact]
    public async Task LoadAsync_OnAFreshInstall_ShouldReportNeitherResumingNorAnExistingProfile()
    {
        // Arrange
        var vm = new TestIntroViewModel(new FakeProfileRepository());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.AnsweredCount.Should().Be(0);
        vm.IsResuming.Should().BeFalse();
        vm.HasExistingProfile.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WithSomeButNotAllItemsAnswered_ShouldReportResuming()
    {
        // Arrange
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(Enumerable.Range(1, 12).Select(n => new KeyValuePair<int, int>(n, 3)));
        var vm = new TestIntroViewModel(repository);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.AnsweredCount.Should().Be(12);
        vm.IsResuming.Should().BeTrue();
        vm.HasExistingProfile.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenAProfileAlreadyExists_ShouldReportAnExistingProfileAndNotResuming()
    {
        // Arrange — a finished test: every item answered, and the profile already scored.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(IpipItemBank.Items.Select(i => new KeyValuePair<int, int>(i.Number, 3)));
        await repository.SaveProfileAsync(new OceanProfile(
            TraitScore.From(50), TraitScore.From(50), TraitScore.From(50),
            TraitScore.From(50), TraitScore.From(50)));
        var vm = new TestIntroViewModel(repository);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.HasExistingProfile.Should().BeTrue();
        vm.IsResuming.Should().BeFalse();
        vm.ShouldShowProfile.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WithAllItemsAnsweredButNoProfileScoredYet_ShouldNotShowProfile()
    {
        // Arrange — every item answered but ProfileViewModel.LoadAsync has never run, so no
        // profile has been scored and saved yet. The Profile tab must still land on this screen,
        // never speculatively on ProfilePage.
        var repository = new FakeProfileRepository();
        repository.SeedAnswers(IpipItemBank.Items.Select(i => new KeyValuePair<int, int>(i.Number, 3)));
        var vm = new TestIntroViewModel(repository);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.ShouldShowProfile.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_OnAFreshInstall_ShouldNotShowProfile()
    {
        // Arrange
        var vm = new TestIntroViewModel(new FakeProfileRepository());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.ShouldShowProfile.Should().BeFalse();
    }
}
