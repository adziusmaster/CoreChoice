using System.Threading.RateLimiting;
using CoreChoice.Ai;
using CoreChoice.Server.Data;
using CoreChoice.Server.Endpoints;
using CoreChoice.Server.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Options --------------------------------------------------------------------------------
builder.Services.Configure<CoinOptions>(builder.Configuration.GetSection(CoinOptions.SectionName));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));

// ---- Database -------------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Db")
    ?? "Data Source=/data/corechoice.server.db";
builder.Services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(connectionString));

// ---- Stores ---------------------------------------------------------------------------------
builder.Services.AddScoped<ICoinStore, SqliteCoinStore>();
builder.Services.AddScoped<IPromptStore, SqlitePromptStore>();
builder.Services.AddScoped<IGrantPolicy, SqliteGrantPolicy>();
builder.Services.AddScoped<IPromoStore, SqlitePromoStore>();

// ---- Origin hashing -------------------------------------------------------------------------
// The cap must recognise a repeat origin without retaining addresses. A configured salt keeps the
// cap effective across restarts; without one, the cap silently resets every deploy, which is a
// production-grade abuse hole rather than a cosmetic gap, so Production refuses to boot without one.
// Outside Production, a random per-boot salt lets a developer run the service unconfigured.
var configuredIpSalt = builder.Configuration["Security:IpHashSalt"];
var ipSaltWasConfigured = !string.IsNullOrWhiteSpace(configuredIpSalt);
string ipSalt;
if (ipSaltWasConfigured)
{
    ipSalt = configuredIpSalt!;
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "Configuration key 'Security:IpHashSalt' is required in Production. Without a stable " +
        "salt, the free-coin origin cap cannot recognise a repeat origin across restarts and " +
        "silently resets on every deploy.");
}
else
{
    ipSalt = Guid.NewGuid().ToString("N");
}
builder.Services.AddSingleton<IClientIpHasher>(new ClientIpHasher(ipSalt));

// ---- Gemini ---------------------------------------------------------------------------------
// No key configured means the fake client, so the whole service runs locally without one.
var geminiKey = builder.Configuration[$"{GeminiOptions.SectionName}:ApiKey"];
if (!string.IsNullOrWhiteSpace(geminiKey))
{
    builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(c =>
    {
        c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        c.Timeout = TimeSpan.FromSeconds(60);
    });
}
else
{
    builder.Services.AddSingleton<IGeminiClient, FakeGeminiClient>();
}

// ---- Rate limiting --------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // An analysis costs a Gemini call, so it gets the tightest budget.
    options.AddPolicy(RateLimitPolicies.Decide, PerClient(limit: 10, window: TimeSpan.FromMinutes(1)));
    // Coin routes are cheap but are the ones worth farming, so they are still bounded.
    options.AddPolicy(RateLimitPolicies.Coins, PerClient(limit: 30, window: TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Personas, PerClient(limit: 60, window: TimeSpan.FromMinutes(1)));
    // Codes are short and human-typeable (5 chars over a 32-symbol alphabet), which makes them
    // guessable by brute force; this is the tightest budget alongside Decide so an unlimited
    // redeem endpoint cannot be turned into a free-coin oracle.
    options.AddPolicy(RateLimitPolicies.Promo, PerClient(limit: 10, window: TimeSpan.FromMinutes(1)));

    static Func<HttpContext, RateLimitPartition<string>> PerClient(int limit, TimeSpan window) =>
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = window, QueueLimit = 0 });
});

builder.Services.AddHostedService<UsageLogRetentionService>();

var app = builder.Build();

if (!ipSaltWasConfigured)
{
    app.Logger.LogWarning(
        "Security:IpHashSalt is not configured; using a random salt for this process. The " +
        "free-coin origin cap will reset on restart.");
}

// ---- Forwarded headers ----------------------------------------------------------------------
// Caddy terminates TLS and proxies to this container. Without this, every request appears to come
// from the proxy: the rate limiter becomes one global bucket and the origin cap sees the whole
// internet as a single origin.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
// The reverse proxy shares this container's Docker network and its address is not fixed, so private
// ranges are trusted for the forwarded header. The container is not otherwise publicly reachable.
forwarded.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
forwarded.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
forwarded.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
app.UseForwardedHeaders(forwarded);

app.UseRateLimiter();

// ---- Routes ---------------------------------------------------------------------------------
app.MapPost("/api/decisions", DecisionEndpoint.Generate)
    .RequireRateLimiting(RateLimitPolicies.Decide);

app.MapGet("/api/coins/{deviceId:guid}", CoinsEndpoint.GetBalance)
    .RequireRateLimiting(RateLimitPolicies.Coins);
app.MapPost("/api/coins/ensure", CoinsEndpoint.Ensure)
    .RequireRateLimiting(RateLimitPolicies.Coins);
app.MapPost("/api/coins/profile-grant", CoinsEndpoint.GrantProfileCompletion)
    .RequireRateLimiting(RateLimitPolicies.Coins);

app.MapGet("/api/personas", PersonasEndpoint.List)
    .RequireRateLimiting(RateLimitPolicies.Personas);

app.MapPost("/api/promo/redeem", PromoEndpoint.Redeem)
    .RequireRateLimiting(RateLimitPolicies.Promo);

// Registered only when a secret is configured: mapping it unconditionally and relying on the
// endpoint filter to 404 leaks the route's existence through argument-binding failures (400 on a
// malformed body) and wrong-verb requests (405), both of which run before the filter does. An
// unset secret should mean the path genuinely does not exist.
if (!string.IsNullOrWhiteSpace(app.Configuration["Dev:Secret"]))
{
    app.MapPost("/api/dev/grant", DevEndpoint.Grant).AddEndpointFilter(DevEndpoint.SecretFilter);
}

// Same reasoning as the dev route above: an unset Admin:Secret must mean these routes do not
// exist at all, not merely that the filter will refuse them.
if (!string.IsNullOrWhiteSpace(app.Configuration["Admin:Secret"]))
{
    app.MapPost("/api/admin/promo", PromoEndpoint.Create).AddEndpointFilter(PromoEndpoint.SecretFilter);
    app.MapGet("/api/admin/promo", PromoEndpoint.List).AddEndpointFilter(PromoEndpoint.SecretFilter);
    app.MapPost("/api/admin/promo/{code}/revoke", PromoEndpoint.Revoke).AddEndpointFilter(PromoEndpoint.SecretFilter);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<ServerDbContext>>());

app.Run();

/// <summary>Named rate-limit policies, so registration cannot drift from the definitions.</summary>
internal static class RateLimitPolicies
{
    public const string Decide = "decide";
    public const string Coins = "coins";
    public const string Personas = "personas";
    public const string Promo = "promo";
}

/// <summary>Exposed so the integration tests can host the real application.</summary>
public partial class Program;
