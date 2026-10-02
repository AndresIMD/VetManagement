using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Self-contained config so tests don't depend on the developer's user-secrets
        // (CI has none). The DB connection string is never used: the DbContext is swapped for InMemory below.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused;Database=unused");
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Jwt:Issuer", "VetManagement.Tests");
        builder.UseSetting("Jwt:Audience", "VetManagement.Tests");
        builder.UseSetting("AllowedOrigins:0", "https://localhost");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase("TestDb"));
        });
    }
}
