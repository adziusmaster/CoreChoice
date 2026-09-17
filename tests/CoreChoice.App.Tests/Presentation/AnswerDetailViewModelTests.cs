using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;
using NSubstitute;

namespace CoreChoice.App.Tests.Presentation;

public class AnswerDetailViewModelTests
{
    private static PastDecision BuildDecision(long id = 1, int confidence = 82, bool personalized = true) => new(
        id,
        Dilemma.Create("Quit my job", "Stay put", "Eight months of savings."),
        PersonaId.From("the-long-view"),
        DecisionWeight.From(4),
        new DecisionAnalysis("Keep the job.", confidence, ["Because"],
            new OptionAssessment("Quit my job", ["Growth"], ["No income"]),
            new OptionAssessment("Stay put", ["Stability"], ["Slower"]),
            "Your openness will make leaping look like adventure.", personalized),
        new DateTimeOffset(2026, 9, 3, 14, 30, 0, TimeSpan.Zero));

    private static IDecisionHistory StubHistory(PastDecision? decision)
    {
        var history = Substitute.For<IDecisionHistory>();
        history.FindAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(decision);
        return history;
    }

    [Fact]
    public async Task LoadAsync_WhenFound_ShouldExposeEveryPartOfTheAnswer()
    {
        // Arrange
        var decision = BuildDecision();
        var vm = new AnswerDetailViewModel(StubHistory(decision));

        // Act
        await vm.LoadAsync(1);

        // Assert
        vm.HasDecision.Should().BeTrue();
        vm.NotFound.Should().BeFalse();
        vm.Analysis.Should().Be(decision.Analysis);
        vm.Verdict.Should().Be("Clear direction");
        vm.Verdict.Any(char.IsDigit).Should().BeFalse();
        vm.IsVerdictStrong.Should().BeTrue();
        vm.PersonaDisplayName.Should().Be("The Long View");
        vm.WeightLabel.Should().Be("Quite a lot");
        vm.OptionA.Should().Be("Quit my job");
        vm.OptionB.Should().Be("Stay put");
        vm.Context.Should().Be("Eight months of savings.");
        vm.HasContext.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WhenNotFound_ShouldExposeNotFoundWithoutThrowing()
    {
        // Arrange — the entry was deleted elsewhere (or the id never resolved), so the page must
        // show a plain message rather than crash on a null decision.
        var vm = new AnswerDetailViewModel(StubHistory(null));

        // Act
        await vm.LoadAsync(999);

        // Assert
        vm.HasDecision.Should().BeFalse();
        vm.NotFound.Should().BeTrue();
        vm.Analysis.Should().BeNull();
        vm.Verdict.Should().BeEmpty();
    }

    [Fact]
    public async Task Context_WhenTheDilemmaHasNone_ShouldBeEmptyNotNull()
    {
        // Arrange
        var decision = new PastDecision(
            1, Dilemma.Create("A", "B"), PersonaId.From("gut-check"), DecisionWeight.From(1),
            BuildDecision().Analysis, DateTimeOffset.UtcNow);
        var vm = new AnswerDetailViewModel(StubHistory(decision));

        // Act
        await vm.LoadAsync(1);

        // Assert
        vm.Context.Should().Be(string.Empty);
        vm.HasContext.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveExactlyTheLoadedDecision()
    {
        // Arrange
        var decision = BuildDecision(id: 42);
        var history = StubHistory(decision);
        var vm = new AnswerDetailViewModel(history);
        await vm.LoadAsync(42);

        // Act
        await vm.DeleteAsync();

        // Assert
        await history.Received(1).DeleteAsync(42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenNothingHasBeenLoaded_ShouldDoNothing()
    {
        // Arrange
        var history = StubHistory(null);
        var vm = new AnswerDetailViewModel(history);

        // Act
        await vm.DeleteAsync();

        // Assert
        await history.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }
}
