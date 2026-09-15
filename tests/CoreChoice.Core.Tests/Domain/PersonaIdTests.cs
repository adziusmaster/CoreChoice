using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.Core.Tests.Domain;

public class PersonaIdTests
{
    [Theory]
    [InlineData("pure-logic")]
    [InlineData("devils-advocate")]
    [InlineData("gut-check")]
    public void From_WithAWellFormedSlug_ShouldAccept(string slug)
    {
        // Act
        var id = PersonaId.From(slug);

        // Assert
        id.Value.Should().Be(slug);
    }

    [Fact]
    public void From_ShouldLowercaseAndTrim()
    {
        // Act
        var id = PersonaId.From("  Pure-Logic  ");

        // Assert
        id.Value.Should().Be("pure-logic");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("has spaces")]
    [InlineData("has_underscore")]
    [InlineData("Robert'); DROP TABLE Personas;--")]
    public void From_WithAMalformedSlug_ShouldThrow(string slug)
    {
        // Act
        var act = () => PersonaId.From(slug);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryFrom_WithAMalformedSlug_ShouldReturnFalseWithoutThrowing()
    {
        // Act
        var ok = PersonaId.TryFrom("not a slug", out var id);

        // Assert
        ok.Should().BeFalse();
        id.Value.Should().BeNull();
    }
}
