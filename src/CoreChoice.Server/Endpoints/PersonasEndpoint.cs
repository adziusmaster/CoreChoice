using CoreChoice.Server.Services;

namespace CoreChoice.Server.Endpoints;

internal static class PersonasEndpoint
{
    /// <summary>
    /// The picker's contents. Returns names and descriptions only — the prompt templates are the
    /// product's actual intellectual property and have no business on the wire.
    /// </summary>
    public static async Task<IResult> List(IPromptStore prompts, CancellationToken ct)
    {
        var personas = await prompts.GetActivePersonasAsync(ct);

        return Results.Ok(personas
            .Select(p => new PersonaResponse(p.Id, p.DisplayName, p.Description))
            .ToList());
    }
}
