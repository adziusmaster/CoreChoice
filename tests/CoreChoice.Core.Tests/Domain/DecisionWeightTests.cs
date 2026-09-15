using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.Core.Tests.Domain;

public class DecisionWeightTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void From_WithinRange_ShouldHoldTheValue(int value)
    {
        // Act
        var weight = DecisionWeight.From(value);

        // Assert
        weight.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-3)]
    public void From_OutsideRange_ShouldThrow(int value)
    {
        // Act
        var act = () => DecisionWeight.From(value);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Description_ForEachValidValue_ShouldBeNonEmpty()
    {
        // Arrange
        var weights = Enumerable.Range(1, 5).Select(DecisionWeight.From);

        // Act
        var descriptions = weights.Select(w => w.Description).ToList();

        // Assert
        descriptions.Should().OnlyHaveUniqueItems();
        // A method group cannot convert to an Expression<Func<string,bool>>, so the predicate is
        // written out. NotContain keeps the failure message that All().BeTrue() throws away —
        // "Expected true, found false" names neither the offending value nor the collection.
        descriptions.Should().NotContain(d => string.IsNullOrWhiteSpace(d));
    }
}
