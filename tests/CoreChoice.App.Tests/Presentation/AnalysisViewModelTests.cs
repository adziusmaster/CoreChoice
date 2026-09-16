using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoreChoice.App.Tests.Presentation;

public class AnalysisViewModelTests
{
    private static DecisionRequest BuildRequest(OceanProfile? profile = null) => new(
        Dilemma.Create("Take the job", "Stay put"),
        profile ?? OceanProfile.None,
        PersonaId.From("the-long-view"),
        DecisionWeight.From(3));

    private static OceanProfile ProfileWithOpenness(int openness) => new(
        TraitScore.From(openness), TraitScore.From(50), TraitScore.From(50), TraitScore.From(50), TraitScore.From(50));

    private static DecisionAnalysis BuildAnalysis(int confidence) => new(
        Recommendation: "Take the job.",
        Confidence: confidence,
        Reasoning: ["First reason.", "Second reason."],
        OptionA: new OptionAssessment("A", ["Strength A"], ["Risk A"]),
        OptionB: new OptionAssessment("B", ["Strength B"], ["Risk B"]),
        PersonalityNote: "Because it is you asking.",
        IsPersonalized: true);

    private static IDecisionClient StubClient(AnalysedDecision result)
    {
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>()).Returns(result);
        return client;
    }

    // ===== Successful call =====

    [Fact]
    public async Task AskAsync_OnSuccess_ShouldExposeAnalysisBalanceAndAStrongCaseVerdict()
    {
        // Arrange
        var analysis = BuildAnalysis(confidence: 75);
        var client = StubClient(new AnalysedDecision(analysis, Balance: 9));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.Analysis.Should().Be(analysis);
        vm.Balance.Should().Be(9);
        vm.Verdict.Should().Be("Strong case");
        vm.HasAnalysis.Should().BeTrue();
        vm.HasError.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task AskAsync_OnSuccess_ShouldLeaveIsWorkingFalseAfterCompleting()
    {
        // Arrange
        var client = StubClient(new AnalysedDecision(BuildAnalysis(60), Balance: 4));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.IsWorking.Should().BeFalse();
    }

    [Fact]
    public async Task AskAsync_WhileTheCallIsPending_IsWorkingShouldBeTrue()
    {
        // Arrange
        var gate = new TaskCompletionSource<AnalysedDecision>();
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = new AnalysisViewModel(client);

        // Act
        var askTask = vm.AskAsync(BuildRequest());

        // Assert — still pending, IsWorking must already be true.
        vm.IsWorking.Should().BeTrue();

        gate.SetResult(new AnalysedDecision(BuildAnalysis(60), Balance: 1));
        await askTask;
        vm.IsWorking.Should().BeFalse();
    }

    [Fact]
    public async Task AskAsync_SetsTheWaitingLineBeforeTheCallResolves()
    {
        // Arrange — the waiting line must come from the local profile, computed before the network
        // call is even awaited, not from anything the (still-pending) response could provide.
        var gate = new TaskCompletionSource<AnalysedDecision>();
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = new AnalysisViewModel(client);

        // Act
        var askTask = vm.AskAsync(BuildRequest(ProfileWithOpenness(90)));

        // Assert
        vm.WaitingLine.Should().Contain("openness");

        gate.SetResult(new AnalysedDecision(BuildAnalysis(60), Balance: 1));
        await askTask;
    }

    // ===== Error mapping sweep =====

    public static TheoryData<Exception, string, bool, bool> FailureCases()
    {
        var data = new TheoryData<Exception, string, bool, bool>();
        data.Add(new InsufficientCoinsException(required: 1, available: 0), "You are out of analyses.", false, true);
        data.Add(new DecisionUnavailableException("offline"),
            "The advisor could not be reached. Your coin was not spent.", true, false);
        data.Add(new MalformedAdvisorResponseException("bad json"),
            "That came back unusable. Your coin was not spent.", true, false);
        data.Add(new InvalidOperationException("anything else"),
            "Something went wrong. Your coin was not spent.", true, false);
        data.Add(new HttpRequestException("dns failure"),
            "Something went wrong. Your coin was not spent.", true, false);
        return data;
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task AskAsync_ForEveryDocumentedFailure_ShouldProduceTheExactMessageAndRetryFlag(
        Exception thrown, string expectedMessage, bool expectedCanRetry, bool expectedIsOutOfCoins)
    {
        // Arrange
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>()).ThrowsForAnyArgs(thrown);
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.ErrorMessage.Should().Be(expectedMessage);
        vm.CanRetry.Should().Be(expectedCanRetry);
        vm.IsOutOfCoins.Should().Be(expectedIsOutOfCoins);
        vm.HasError.Should().BeTrue();
        vm.HasAnalysis.Should().BeFalse();
        vm.IsWorking.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task AskAsync_ForEveryFailureExceptInsufficientCoins_ShouldTellThePersonTheCoinWasSafe(
        Exception thrown, string expectedMessage, bool expectedCanRetry, bool expectedIsOutOfCoins)
    {
        // Arrange — this is the invariant that matters most: a future edit to any one of these
        // messages that drops the reassurance must fail this test, not just the exact-string one.
        _ = expectedMessage;
        _ = expectedCanRetry;
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>()).ThrowsForAnyArgs(thrown);
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        if (expectedIsOutOfCoins)
        {
            vm.ErrorMessage.Should().NotContain("coin was not spent",
                "InsufficientCoinsException has nothing to refund — there is no coin-safety claim to make");
        }
        else
        {
            vm.ErrorMessage.Should().Contain("coin was not spent",
                $"a retryable failure must explicitly say the coin is safe, but got: \"{vm.ErrorMessage}\"");
        }
    }

    [Fact]
    public async Task AskAsync_WhenTheClientThrows_ShouldStillLeaveIsWorkingFalse()
    {
        // Arrange
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsForAnyArgs(new DecisionUnavailableException("down"));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.IsWorking.Should().BeFalse();
    }

    [Fact]
    public async Task AskAsync_WhenTheClientThrowsAnUnrecognizedException_ShouldNotLeakItsMessage()
    {
        // Arrange — guards the catch-all: a bare exception message must never reach the screen.
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsForAnyArgs(new InvalidOperationException("NullReferenceException at CoreChoiceApiClient.cs:42"));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.ErrorMessage.Should().Be("Something went wrong. Your coin was not spent.");
        vm.ErrorMessage.Should().NotContain("NullReferenceException");
        vm.ErrorMessage.Should().NotContain("CoreChoiceApiClient.cs");
    }

    // ===== Cancellation =====

    [Fact]
    public async Task AskAsync_WhenTheCallerCancels_ShouldPropagateCancellationRatherThanSetAnErrorMessage()
    {
        // Arrange
        var client = Substitute.For<IDecisionClient>();
        using var cts = new CancellationTokenSource();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>()).ThrowsForAnyArgs(_ =>
        {
            cts.Cancel();
            return new OperationCanceledException(cts.Token);
        });
        var vm = new AnalysisViewModel(client);

        // Act
        Func<Task> act = async () => await vm.AskAsync(BuildRequest(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        vm.ErrorMessage.Should().BeNull();
        vm.IsWorking.Should().BeFalse();
    }

    // ===== Verdict =====

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(99)]
    [InlineData(100)]
    public async Task Verdict_AcrossTheFullConfidenceRange_ShouldNeverContainADigit(int confidence)
    {
        // Arrange
        var client = StubClient(new AnalysedDecision(BuildAnalysis(confidence), Balance: 3));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.Verdict.Should().NotContainAny("0", "1", "2", "3", "4", "5", "6", "7", "8", "9");
    }

    public static IEnumerable<object[]> EveryConfidenceValue() =>
        Enumerable.Range(0, 101).Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(EveryConfidenceValue))]
    public async Task Verdict_SweptAcrossEveryPossibleConfidenceValue_ShouldNeverContainADigit(int confidence)
    {
        // Arrange
        var client = StubClient(new AnalysedDecision(BuildAnalysis(confidence), Balance: 0));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest());

        // Assert
        vm.Verdict.Any(char.IsDigit).Should().BeFalse($"Verdict for confidence {confidence} was \"{vm.Verdict}\"");
    }

    // ===== Waiting line wiring (composer itself is swept separately) =====

    [Fact]
    public async Task AskAsync_WithAProfile_ShouldNameATraitInTheWaitingLine()
    {
        // Arrange
        var client = StubClient(new AnalysedDecision(BuildAnalysis(60), Balance: 1));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest(ProfileWithOpenness(10)));

        // Assert
        vm.WaitingLine.Should().Contain("openness");
    }

    [Fact]
    public async Task AskAsync_WithNoProfile_ShouldNotNameAnyTraitInTheWaitingLine()
    {
        // Arrange
        var client = StubClient(new AnalysedDecision(BuildAnalysis(60), Balance: 1));
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.AskAsync(BuildRequest(OceanProfile.None));

        // Assert
        vm.WaitingLine.Should().NotContainAny(
            "openness", "conscientiousness", "extraversion", "agreeableness", "neuroticism");
    }

    // ===== Retry =====

    [Fact]
    public async Task RetryAsync_AfterAFailure_ShouldResendTheSameRequest()
    {
        // Arrange
        var request = BuildRequest();
        var client = Substitute.For<IDecisionClient>();
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsForAnyArgs(new DecisionUnavailableException("down"));
        var vm = new AnalysisViewModel(client);
        await vm.AskAsync(request);
        client.ClearReceivedCalls();

        // Re-stub for the retry: NSubstitute uses the most recently configured matching call.
        client.AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnalysedDecision(BuildAnalysis(70), Balance: 5));

        // Act
        await vm.RetryAsync();

        // Assert
        await client.Received(1).AnalyseAsync(request, Arg.Any<CancellationToken>());
        vm.HasAnalysis.Should().BeTrue();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RetryAsync_WhenNothingHasBeenAskedYet_ShouldDoNothing()
    {
        // Arrange
        var client = Substitute.For<IDecisionClient>();
        var vm = new AnalysisViewModel(client);

        // Act
        await vm.RetryAsync();

        // Assert
        await client.DidNotReceive().AnalyseAsync(Arg.Any<DecisionRequest>(), Arg.Any<CancellationToken>());
        vm.HasAnalysis.Should().BeFalse();
    }
}
