using CoreChoice.Ai;
using CoreChoice.Server.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoreChoice.Server.Tests;

/// <summary>
/// Hosts the real application over an in-memory SQLite database and a caller-supplied Gemini client.
/// Real routing, real DI, real middleware: an endpoint test that stubs the host proves only that the
/// handler compiles.
/// </summary>
internal sealed class CoreChoiceAppFactory(IGeminiClient gemini) : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureServices(services =>
        {
            _connection.Open();

            services.RemoveAll(typeof(IDbContextFactory<ServerDbContext>));
            services.RemoveAll(typeof(DbContextOptions<ServerDbContext>));
            services.AddDbContextFactory<ServerDbContext>(o => o.UseSqlite(_connection));

            services.RemoveAll(typeof(IGeminiClient));
            services.AddSingleton(gemini);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
