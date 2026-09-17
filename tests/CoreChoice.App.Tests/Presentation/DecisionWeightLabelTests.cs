using CoreChoice.Presentation;
using FluentAssertions;

namespace CoreChoice.App.Tests.Presentation;

public class DecisionWeightLabelTests
{
    [Theory]
    [InlineData(1, "Barely anything")]
    [InlineData(2, "Not much")]
    [InlineData(3, "A fair amount")]
    [InlineData(4, "Quite a lot")]
    [InlineData(5, "A great deal")]
    public void For_EachDocumentedWeight_ShouldReturnItsLabel(int weight, string expected)
    {
        // Act
        var label = DecisionWeightLabel.For(weight);

        // Assert
        label.Should().Be(expected);
    }
}
