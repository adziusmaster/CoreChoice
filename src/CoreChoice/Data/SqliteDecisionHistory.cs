using System.Text.Json;
using CoreChoice.Application;
using CoreChoice.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Data;

/// <summary>
/// The device's own record of every decision it has answered. No MAUI type appears here: the
/// net10.0 test project links this file by source, the same discipline as
/// <see cref="SqliteProfileRepository"/>, and a MAUI reference would put it out of reach of tests
/// entirely.
/// </summary>
internal sealed class SqliteDecisionHistory(IDbContextFactory<LocalDbContext> factory) : IDecisionHistory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PastDecision>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var rows = await db.Decisions.AsNoTracking()
            .OrderByDescending(x => x.AskedAt)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<PastDecision?> FindAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var row = await db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }

    public async Task<long> RecordAsync(
        Dilemma dilemma,
        PersonaId persona,
        DecisionWeight weight,
        DecisionAnalysis analysis,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var row = new DecisionRow
        {
            OptionA = dilemma.OptionA,
            OptionB = dilemma.OptionB,
            Context = dilemma.Context,
            PersonaId = persona.Value,
            Weight = weight.Value,
            Recommendation = analysis.Recommendation,
            Confidence = analysis.Confidence,
            ReasoningJson = JsonSerializer.Serialize(analysis.Reasoning, Json),
            OptionAJson = JsonSerializer.Serialize(analysis.OptionA, Json),
            OptionBJson = JsonSerializer.Serialize(analysis.OptionB, Json),
            PersonalityNote = analysis.PersonalityNote,
            IsPersonalized = analysis.IsPersonalized,
            AskedAt = DateTimeOffset.UtcNow,
        };

        db.Decisions.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Decisions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    private static PastDecision Map(DecisionRow row) => new(
        row.Id,
        Dilemma.Create(row.OptionA, row.OptionB, row.Context),
        Domain.PersonaId.From(row.PersonaId),
        DecisionWeight.From(row.Weight),
        new DecisionAnalysis(
            row.Recommendation,
            row.Confidence,
            JsonSerializer.Deserialize<List<string>>(row.ReasoningJson, Json) ?? [],
            JsonSerializer.Deserialize<OptionAssessment>(row.OptionAJson, Json)
                ?? new OptionAssessment(row.OptionA, [], []),
            JsonSerializer.Deserialize<OptionAssessment>(row.OptionBJson, Json)
                ?? new OptionAssessment(row.OptionB, [], []),
            row.PersonalityNote,
            row.IsPersonalized),
        row.AskedAt);
}
