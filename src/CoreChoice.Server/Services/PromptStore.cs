using CoreChoice.Domain;
using CoreChoice.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Services;

internal sealed record PersonaView(string Id, string DisplayName, string Description, int SortOrder);

internal sealed record ActivePrompt(string Template, int Version);

internal interface IPromptStore
{
    Task<IReadOnlyList<PersonaView>> GetActivePersonasAsync(CancellationToken ct = default);

    /// <summary>The active template for a persona, or null when the persona is unknown or inactive.</summary>
    Task<ActivePrompt?> GetActivePromptAsync(PersonaId persona, CancellationToken ct = default);
}

internal sealed class SqlitePromptStore(IDbContextFactory<ServerDbContext> factory) : IPromptStore
{
    public async Task<IReadOnlyList<PersonaView>> GetActivePersonasAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Projected and materialized here: no IQueryable leaves this method.
        return await db.Personas.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PersonaView(p.Id, p.DisplayName, p.Description, p.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<ActivePrompt?> GetActivePromptAsync(PersonaId persona, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var id = persona.Value;

        return await db.PromptTemplates.AsNoTracking()
            .Where(t => t.PersonaId == id && t.IsActive)
            .Join(db.Personas.Where(p => p.IsActive), t => t.PersonaId, p => p.Id, (t, _) => t)
            .Select(t => new ActivePrompt(t.Template, t.Version))
            .FirstOrDefaultAsync(ct);
    }
}
