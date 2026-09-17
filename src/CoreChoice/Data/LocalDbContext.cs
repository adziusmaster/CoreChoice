using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Data;

/// <summary>One answered IPIP item. <see cref="ItemNumber"/> is the primary key, so re-answering
/// the same item upserts rather than accumulating rows.</summary>
internal sealed class AnswerRow
{
    public int ItemNumber { get; set; }

    public int Response { get; set; }

    public DateTimeOffset AnsweredAt { get; set; }
}

/// <summary>The scored profile. A single row keyed at <see cref="Id"/> = 1, because there is one
/// person per phone.</summary>
internal sealed class ProfileRow
{
    public int Id { get; set; } = 1;

    public int Openness { get; set; }

    public int Conscientiousness { get; set; }

    public int Extraversion { get; set; }

    public int Agreeableness { get; set; }

    public int Neuroticism { get; set; }

    public DateTimeOffset ScoredAt { get; set; }
}


/// <summary>
/// One answered decision, kept on the device for as long as the person keeps the app. The three
/// list-shaped parts of an analysis (the reasoning, and each option's strengths and risks) are held
/// as JSON in a single column rather than in child tables: they are only ever read and written
/// whole, never queried across, and three extra tables would buy nothing but joins.
/// </summary>
internal sealed class DecisionRow
{
    public long Id { get; set; }

    public string OptionA { get; set; } = string.Empty;

    public string OptionB { get; set; } = string.Empty;

    public string? Context { get; set; }

    public string PersonaId { get; set; } = string.Empty;

    public int Weight { get; set; }

    public string Recommendation { get; set; } = string.Empty;

    public int Confidence { get; set; }

    /// <summary>The reasoning points, as a JSON array of strings.</summary>
    public string ReasoningJson { get; set; } = "[]";

    /// <summary>Option A's strengths and risks, as a JSON object.</summary>
    public string OptionAJson { get; set; } = "{}";

    public string OptionBJson { get; set; } = "{}";

    public string PersonalityNote { get; set; } = string.Empty;

    public bool IsPersonalized { get; set; }

    public DateTimeOffset AskedAt { get; set; }
}

/// <summary>
/// On-device storage for the personality test. No MAUI types belong here: the database path is
/// supplied by the caller through <see cref="DbContextOptions{TContext}"/>, so this class (and the
/// test project that links it by source) never needs to resolve <c>FileSystem.AppDataDirectory</c>.
/// </summary>
internal sealed class LocalDbContext(DbContextOptions<LocalDbContext> options) : DbContext(options)
{
    public DbSet<AnswerRow> Answers => Set<AnswerRow>();

    public DbSet<ProfileRow> Profiles => Set<ProfileRow>();

    public DbSet<DecisionRow> Decisions => Set<DecisionRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // UTC ticks, not DateTimeOffset, on every date column: EF Core's SQLite provider cannot
        // translate DateTimeOffset comparisons, and left as DateTimeOffset a filter silently moves
        // client-side. Mirrors CoreChoice.Server's ServerDbContext.
        b.Entity<AnswerRow>(e =>
        {
            e.HasKey(x => x.ItemNumber);
            e.Property(x => x.AnsweredAt).HasConversion(Ticks.To, Ticks.From);
        });

        b.Entity<ProfileRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ScoredAt).HasConversion(Ticks.To, Ticks.From);
        });

        b.Entity<DecisionRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.AskedAt).HasConversion(Ticks.To, Ticks.From);
            // History is listed newest-first on every visit, so the sort is the access pattern.
            e.HasIndex(x => x.AskedAt);
        });
    }

    private static class Ticks
    {
        public static readonly Expression<Func<DateTimeOffset, long>> To =
            v => v.UtcTicks;

        public static readonly Expression<Func<long, DateTimeOffset>> From =
            v => new DateTimeOffset(v, TimeSpan.Zero);
    }
}
