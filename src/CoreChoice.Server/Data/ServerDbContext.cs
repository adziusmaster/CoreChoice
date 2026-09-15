using Microsoft.EntityFrameworkCore;

namespace CoreChoice.Server.Data;

internal sealed class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
{
    public DbSet<DeviceCoins> Coins => Set<DeviceCoins>();
    public DbSet<DeviceSeed> DeviceSeeds => Set<DeviceSeed>();
    public DbSet<ProfileGrant> ProfileGrants => Set<ProfileGrant>();
    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<DeviceCoins>().HasKey(x => x.DeviceId);

        // UTC ticks, not DateTimeOffset: EF Core's SQLite provider cannot translate DateTimeOffset
        // comparisons, and both the cap query and the retention sweep filter on these columns. Left
        // as DateTimeOffset, the filter silently moves client-side and loads the whole table.
        b.Entity<DeviceSeed>(e =>
        {
            e.HasKey(x => x.DeviceId);
            e.Property(x => x.SeededAt).HasConversion(Ticks.To, Ticks.From);
            e.HasIndex(x => new { x.IpHash, x.SeededAt });
        });

        b.Entity<ProfileGrant>(e =>
        {
            e.HasKey(x => x.DeviceId);
            e.Property(x => x.GrantedAt).HasConversion(Ticks.To, Ticks.From);
            e.HasIndex(x => new { x.IpHash, x.GrantedAt });
        });

        b.Entity<UsageLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.At).HasConversion(Ticks.To, Ticks.From);
            e.HasIndex(x => x.At);
        });

        b.Entity<Persona>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.IsActive, x.SortOrder });
        });

        b.Entity<PromptTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).HasConversion(Ticks.To, Ticks.From);
            // One active template per persona. A second active row would make prompt selection
            // depend on row order, which is the kind of bug that only appears after an edit.
            e.HasIndex(x => new { x.PersonaId, x.IsActive })
                .IsUnique()
                .HasFilter("\"IsActive\" = 1");
            e.HasIndex(x => new { x.PersonaId, x.Version }).IsUnique();
        });
    }

    private static class Ticks
    {
        public static readonly System.Linq.Expressions.Expression<Func<DateTimeOffset, long>> To =
            v => v.UtcTicks;

        public static readonly System.Linq.Expressions.Expression<Func<long, DateTimeOffset>> From =
            v => new DateTimeOffset(v, TimeSpan.Zero);
    }
}
