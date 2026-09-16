namespace CoreChoice.Application;

/// <summary>The picker's contents: names and descriptions only, never the prompt template.</summary>
public sealed record PersonaSummary(string Id, string DisplayName, string Description);

/// <summary>
/// Outbound port for the persona picker. A separate interface from <see cref="IDecisionClient"/>
/// and <see cref="ICoinLedgerClient"/> because it is its own conversation with the backend, but it
/// belongs here in the Application layer with its siblings, not next to the client that implements
/// it: an inbound or outbound port is an Application concern regardless of what happens to
/// implement it. It exists so <c>Presentation.PersonaViewModel</c>, a public type bound from XAML,
/// can depend on something other than the <c>internal</c> <c>CoreChoice.Services.CoreChoiceApiClient</c>
/// itself — a public member cannot take a less-accessible type in its signature.
/// </summary>
public interface IPersonaCatalog
{
    Task<IReadOnlyList<PersonaSummary>> GetPersonasAsync(CancellationToken ct = default);
}
