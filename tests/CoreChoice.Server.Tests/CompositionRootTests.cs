using CoreChoice.Server.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoreChoice.Server.Tests;

/// <summary>
/// Guards deploy-time-only behaviour in Program.cs that no endpoint test exercises, because every
/// endpoint test runs under Development via <see cref="CoreChoiceAppFactory"/>.
/// </summary>
public class CompositionRootTests
{
    [Fact]
    public void Boot_InProduction_WithoutSalt_ShouldThrow()
    {
        // Arrange — a Production boot with no Security:IpHashSalt configured must fail fast rather
        // than silently falling back to a per-boot random salt, which would reset the free-coin
        // origin cap on every deploy.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(IDbContextFactory<ServerDbContext>));
                services.RemoveAll(typeof(DbContextOptions<ServerDbContext>));
                services.AddSingleton(InMemoryDb.Create());
            });
        });

        // Act
        Action act = () => _ = factory.Server;

        // Assert — the exception thrown by Program.cs's top-level statements propagates unwrapped.
        act.Should().Throw<InvalidOperationException>().WithMessage("*Security:IpHashSalt*");
    }
}
