using CoreChoice.Server.Data;
using CoreChoice.Server.Endpoints;
using CoreChoice.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Db")
    ?? "Data Source=/data/corechoice.server.db";
builder.Services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(connectionString));

// Interim wiring so Task 13's endpoints have what they need. Task 14 replaces this with the
// full composition root.
builder.Services.Configure<CoinOptions>(builder.Configuration.GetSection(CoinOptions.SectionName));
builder.Services.AddScoped<ICoinStore, SqliteCoinStore>();
builder.Services.AddScoped<IPromptStore, SqlitePromptStore>();
builder.Services.AddScoped<IGrantPolicy, SqliteGrantPolicy>();
builder.Services.AddSingleton<IClientIpHasher>(new ClientIpHasher(
    builder.Configuration["Security:IpHashSalt"] ?? Guid.NewGuid().ToString("N")));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/coins/{deviceId:guid}", CoinsEndpoint.GetBalance);
app.MapPost("/api/coins/ensure", CoinsEndpoint.Ensure);
app.MapPost("/api/coins/profile-grant", CoinsEndpoint.GrantProfileCompletion);
app.MapGet("/api/personas", PersonasEndpoint.List);
app.MapPost("/api/dev/grant", DevEndpoint.Grant).AddEndpointFilter(DevEndpoint.SecretFilter);

await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<ServerDbContext>>());

app.Run();

/// <summary>Exposed so the integration tests can host the real application.</summary>
public partial class Program;
