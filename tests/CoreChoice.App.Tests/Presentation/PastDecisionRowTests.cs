using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class PastDecisionRowTests
{
    private static PastDecision BuildDecision(int confidence) => new(
        5,
        Dilemma.Create("Move to Berlin", "Stay in Rotterdam"),
        PersonaId.From("the-pragmatist"),
        DecisionWeight.From(3),
        new DecisionAnalysis("Move.", confidence, ["Because"],
            new OptionAssessment("Move to Berlin", [], []), new OptionAssessment("Stay in Rotterdam", [], []),
            string.Empty, false),
        new DateTimeOffset(2026, 3, 14, 9, 5, 0, TimeSpan.Zero));

    [Fact]
    public void From_ShouldCarryTheIdAndBothOptions()
    {
        // Arrange & Act
        var row = PastDecisionRow.From(BuildDecision(70));

        // Assert
        row.Id.Should().Be(5);
        row.OptionA.Should().Be("Move to Berlin");
        row.OptionB.Should().Be("Stay in Rotterdam");
    }

    [Fact]
    public void From_ShouldFormatWhenItWasAsked()
    {
        // Arrange & Act
        var row = PastDecisionRow.From(BuildDecision(70));

        // Assert
        row.AskedAtDisplay.Should().Be("Mar 14, 2026 at 9:05 AM");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(100)]
    public void From_TheVerdict_ShouldNeverContainADigit(int confidence)
    {
        // Arrange & Act
        var row = PastDecisionRow.From(BuildDecision(confidence));

        // Assert — this app's rule that a raw confidence number never reaches a screen applies to
        // a reopened past decision exactly as it does to a freshly-answered one.
        row.Verdict.Any(char.IsDigit).Should().BeFalse();
    }

    [Fact]
    public void From_WithAStrongConfidence_ShouldUseTheClearDirectionLabel()
    {
        // Arrange & Act
        var row = PastDecisionRow.From(BuildDecision(85));

        // Assert
        row.Verdict.Should().Be("Clear direction");
    }
}
