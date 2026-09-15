using CoreChoice.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Db")
    ?? "Data Source=/data/corechoice.server.db";
builder.Services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(connectionString));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<ServerDbContext>>());

app.Run();

/// <summary>Exposed so the integration tests can host the real application.</summary>
public partial class Program;
