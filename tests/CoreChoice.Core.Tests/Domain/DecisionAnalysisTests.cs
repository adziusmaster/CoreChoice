using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.Core.Tests.Domain;

public class DecisionAnalysisTests
{
    [Fact]
    public void Empty_ShouldBeAllZeroes()
    {
        // Act
        var usage = TokenUsage.Empty;

        // Assert
        usage.PromptTokens.Should().Be(0);
        usage.OutputTokens.Should().Be(0);
        usage.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void Analysis_ShouldCarryWhetherItWasPersonalized()
    {
        // Arrange
        var assessment = new OptionAssessment("Go", ["clear upside"], ["costly to undo"]);

        // Act
        var analysis = new DecisionAnalysis(
            Recommendation: "Go",
            Confidence: 72,
            Reasoning: ["the upside compounds", "the downside is bounded"],
            OptionA: assessment,
            OptionB: new OptionAssessment("Stay", ["low risk"], ["opportunity cost"]),
            PersonalityNote: "Your high openness favours the unfamiliar option.",
            IsPersonalized: true);

        // Assert
        analysis.IsPersonalized.Should().BeTrue();
        analysis.Reasoning.Should().HaveCount(2);
    }
}
