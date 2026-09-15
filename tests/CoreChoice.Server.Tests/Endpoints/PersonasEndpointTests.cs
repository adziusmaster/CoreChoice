using System.Net;
using System.Net.Http.Json;
using CoreChoice.Ai;
using FluentAssertions;
using NSubstitute;

namespace CoreChoice.Server.Tests.Endpoints;

public class PersonasEndpointTests
{
    [Fact]
    public async Task List_ShouldReturnTheSixSeededPersonasInOrder()
    {
        // Arrange
        var client = new CoreChoiceAppFactory(Substitute.For<IGeminiClient>()).CreateClient();

        // Act
        var response = await client.GetAsync("/api/personas");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var personas = await response.Content.ReadFromJsonAsync<List<PersonaDto>>();
        personas.Should().HaveCount(6);
        personas![0].Id.Should().Be("devils-advocate");
        personas.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.DisplayName));
        personas.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Description));
    }

    [Fact]
    public async Task List_ShouldNotLeakPromptTemplates()
    {
        // Arrange — the prompts are the product's actual IP; the picker needs names, not templates.
        var client = new CoreChoiceAppFactory(Substitute.For<IGeminiClient>()).CreateClient();

        // Act
        var raw = await client.GetStringAsync("/api/personas");

        // Assert
        raw.Should().NotContain("Your stance:");
        raw.Should().NotContain("{{PROFILE}}");
    }

    private sealed record PersonaDto(string Id, string DisplayName, string Description);
}
