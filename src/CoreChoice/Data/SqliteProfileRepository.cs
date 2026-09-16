using CoreChoice.Application;
using CoreChoice.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Data;

/// <summary>
/// SQLite-backed <see cref="IProfileRepository"/>. Answers persist per item, immediately on tap,
/// so a fifty-item test abandoned mid-way survives to the next launch. The profile lives in a
/// single row (<see cref="ProfileRow.Id"/> = 1) that a retake overwrites rather than appends to.
/// </summary>
internal sealed class SqliteProfileRepository(IDbContextFactory<LocalDbContext> contextFactory) : IProfileRepository
{
    public async Task<StoredAnswers> LoadAnswersAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var rows = await db.Answers.AsNoTracking().ToListAsync(ct);
        var updatedAt = rows.Count == 0
            ? DateTimeOffset.MinValue
            : rows.Max(r => r.AnsweredAt);

        return new StoredAnswers(
            rows.ToDictionary(r => r.ItemNumber, r => r.Response),
            updatedAt);
    }

    public async Task SaveAnswerAsync(int itemNumber, int response, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var existing = await db.Answers.FindAsync([itemNumber], ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            db.Answers.Add(new AnswerRow { ItemNumber = itemNumber, Response = response, AnsweredAt = now });
        }
        else
        {
            existing.Response = response;
            existing.AnsweredAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAnswersAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        await db.Answers.ExecuteDeleteAsync(ct);
    }

    public async Task<OceanProfile> LoadProfileAsync(CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var row = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1, ct);

        return row is null
            ? OceanProfile.None
            : new OceanProfile(
                TraitScore.From(row.Openness),
                TraitScore.From(row.Conscientiousness),
                TraitScore.From(row.Extraversion),
                TraitScore.From(row.Agreeableness),
                TraitScore.From(row.Neuroticism));
    }

    public async Task SaveProfileAsync(OceanProfile profile, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var existing = await db.Profiles.FindAsync([1], ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            db.Profiles.Add(new ProfileRow
            {
                Id = 1,
                Openness = profile.Openness.Value,
                Conscientiousness = profile.Conscientiousness.Value,
                Extraversion = profile.Extraversion.Value,
                Agreeableness = profile.Agreeableness.Value,
                Neuroticism = profile.Neuroticism.Value,
                ScoredAt = now,
            });
        }
        else
        {
            existing.Openness = profile.Openness.Value;
            existing.Conscientiousness = profile.Conscientiousness.Value;
            existing.Extraversion = profile.Extraversion.Value;
            existing.Agreeableness = profile.Agreeableness.Value;
            existing.Neuroticism = profile.Neuroticism.Value;
            existing.ScoredAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
