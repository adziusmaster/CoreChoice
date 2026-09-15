using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.Core.Tests.Domain;

public class DilemmaTests
{
    [Fact]
    public void Create_WithTwoOptions_ShouldTrimAndKeepThem()
    {
        // Arrange
        const string a = "  Take the job in Berlin  ";
        const string b = "\tStay in Warsaw\n";

        // Act
        var dilemma = Dilemma.Create(a, b);

        // Assert
        dilemma.OptionA.Should().Be("Take the job in Berlin");
        dilemma.OptionB.Should().Be("Stay in Warsaw");
        dilemma.Context.Should().BeNull();
    }

    [Theory]
    [InlineData("", "Stay")]
    [InlineData("   ", "Stay")]
    [InlineData("Go", "")]
    public void Create_WhenAnOptionIsBlank_ShouldThrow(string a, string b)
    {
        // Act
        var act = () => Dilemma.Create(a, b);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenAnOptionExceedsTheCap_ShouldThrow()
    {
        // Arrange
        var tooLong = new string('x', Dilemma.MaxOptionLength + 1);

        // Act
        var act = () => Dilemma.Create(tooLong, "Stay");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{Dilemma.MaxOptionLength}*");
    }

    [Fact]
    public void Create_WhenContextIsBlank_ShouldNormalizeToNull()
    {
        // Arrange & Act
        var dilemma = Dilemma.Create("Go", "Stay", "   ");

        // Assert
        dilemma.Context.Should().BeNull();
    }
}
