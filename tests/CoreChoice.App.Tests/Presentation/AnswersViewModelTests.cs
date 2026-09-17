using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.App.Tests.Presentation;

public class AnswersViewModelTests
{
    private static PastDecision BuildDecision(long id, string optionA = "A", string optionB = "B", int confidence = 70) => new(
        id,
        Dilemma.Create(optionA, optionB),
        PersonaId.From("the-long-view"),
        DecisionWeight.From(3),
        new DecisionAnalysis("Rec", confidence, ["Because"], new OptionAssessment(optionA, [], []),
            new OptionAssessment(optionB, [], []), string.Empty, false),
        DateTimeOffset.UtcNow);

    private static IDecisionHistory StubHistory(params PastDecision[] decisions)
    {
        var history = Substitute.For<IDecisionHistory>();
        history.ListAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<PastDecision>)decisions);
        return history;
    }

    [Fact]
    public void IsEmpty_BeforeLoadAsyncRuns_ShouldBeTrue()
    {
        // Arrange
        var vm = new AnswersViewModel(StubHistory());

        // Act & Assert — someone who has asked nothing should see the empty state immediately,
        // not a blank screen while the first load is still pending.
        vm.IsEmpty.Should().BeTrue();
        vm.HasDecisions.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WithNoHistory_ShouldExposeTheEmptyState()
    {
        // Arrange
        var vm = new AnswersViewModel(StubHistory());

        // Act
        await vm.LoadAsync();

        // Assert
        vm.IsEmpty.Should().BeTrue();
        vm.Decisions.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WithHistory_ShouldExposeOneRowPerDecisionInTheOrderReturned()
    {
        // Arrange — the port itself already guarantees newest-first; this view model must not
        // re-sort or otherwise disturb that order.
        var newer = BuildDecision(2, "Newer A", "Newer B");
        var older = BuildDecision(1, "Older A", "Older B");
        var vm = new AnswersViewModel(StubHistory(newer, older));

        // Act
        await vm.LoadAsync();

        // Assert
        vm.HasDecisions.Should().BeTrue();
        vm.IsEmpty.Should().BeFalse();
        vm.Decisions.Should().HaveCount(2);
        vm.Decisions[0].Id.Should().Be(2);
        vm.Decisions[0].OptionA.Should().Be("Newer A");
        vm.Decisions[1].Id.Should().Be(1);
    }

    [Theory]
    [InlineData(90, "Clear direction")]
    [InlineData(40, "Genuinely close")]
    public async Task LoadAsync_ShouldExposeTheVerdictLabelNeverTheRawConfidence(int confidence, string expectedVerdict)
    {
        // Arrange
        var vm = new AnswersViewModel(StubHistory(BuildDecision(1, confidence: confidence)));

        // Act
        await vm.LoadAsync();

        // Assert
        vm.Decisions[0].Verdict.Should().Be(expectedVerdict);
        vm.Decisions[0].Verdict.Any(char.IsDigit).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenTheHistoryThrows_ShouldLeaveWhateverWasThereRatherThanCrash()
    {
        // Arrange
        var history = Substitute.For<IDecisionHistory>();
        history.ListAsync(Arg.Any<CancellationToken>()).ThrowsForAnyArgs(new InvalidOperationException("disk error"));
        var vm = new AnswersViewModel(history);

        // Act
        var act = async () => await vm.LoadAsync();

        // Assert
        await act.Should().NotThrowAsync();
        vm.Decisions.Should().BeEmpty();
        vm.IsEmpty.Should().BeTrue();
    }
}
