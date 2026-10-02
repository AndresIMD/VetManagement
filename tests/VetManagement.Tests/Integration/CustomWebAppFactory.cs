using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using VetManagement.Api.Authorization;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    // One in-memory database per factory instance, so test classes never see each other's data.
    private readonly string _databaseName = $"TestDb-{Guid.NewGuid():N}";

    private const string JwtKey = "integration-tests-signing-key-0123456789abcdef";
    private const string JwtIssuer = "VetManagement.Tests";

    /// <summary>Client authenticated as a test user holding the given permission claims (see Api.Authorization.Permissions).</summary>
    public HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim(Permissions.CLAIM_TYPE, p)).Append(new Claim(ClaimTypes.Name, "test-user"));
        var token = new JwtSecurityToken(JwtIssuer, JwtIssuer, claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Self-contained config so tests don't depend on the developer's user-secrets
        // (CI has none). The DB connection string is never used: the DbContext is swapped for InMemory below.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused;Database=unused");
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtIssuer);
        builder.UseSetting("AllowedOrigins:0", "https://localhost");

        builder.ConfigureServices(services =>
        {
            // EF Core 9+ keeps the UseSqlServer(...) configuration in IDbContextOptionsConfiguration<T>;
            // without removing it both providers are registered and any DB access throws.
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(_databaseName));
        });
    }
}
