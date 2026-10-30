using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Connections;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using VetManagement.Api.Authorization;
using VetManagement.Application.Contracts.Services;
using VetManagement.Infrastructure.Data;
using VetManagement.Staff.UI.Services.Realtime;

namespace VetManagement.Tests.Integration;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    // One in-memory database per factory instance, so test classes never see each other's data.
    private readonly string _databaseName = $"TestDb-{Guid.NewGuid():N}";

    /// <summary>Emails the API "sent" during the test.</summary>
    public RecordingEmailSender Emails { get; } = new();

    private const string JwtKey = "integration-tests-signing-key-0123456789abcdef";
    private const string JwtIssuer = "VetManagement.Tests";

    /// <summary>Client authenticated as a test user holding the given permission claims (see Api.Authorization.Permissions).</summary>
    public HttpClient CreateClientWithPermissions(params string[] permissions)
        => CreateClientWithClaims([.. permissions.Select(p => new Claim(Permissions.CLAIM_TYPE, p)), new Claim(ClaimTypes.Name, "test-user")]);

    /// <summary>Client whose token, signed with the API's key, carries exactly these claims.</summary>
    public HttpClient CreateClientWithClaims(IEnumerable<Claim> claims)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(claims));
        return client;
    }

    /// <summary>A token signed with the API's key that carries exactly these claims.</summary>
    public string CreateToken(IEnumerable<Claim> claims)
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(JwtIssuer, JwtIssuer, claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256)));

    /// <summary>The staff app's realtime client, connected to this API's hubs through the in-memory server (call InitializeAsync).</summary>
    public RealtimeService CreateRealtimeClient()
    {
        var token = CreateToken([new Claim(ClaimTypes.Name, "open-screen")]);
        return new RealtimeService(Server.BaseAddress.ToString(), () => Task.FromResult<string?>(token), options =>
        {
            options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
        });
    }

    /// <summary>Database used by the API under test: in-memory by default (fast, no server needed).</summary>
    protected virtual void ConfigureDatabase(DbContextOptionsBuilder options) => options.UseInMemoryDatabase(_databaseName);

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
        // Tests book many times per minute from one "IP"; the default public limit would return 429.
        builder.UseSetting("RateLimits:PublicWritePerMinute", "10000");
        builder.UseSetting("RateLimits:PublicReadPerMinute", "10000");
        // Every client of a test class signs in as the same user; a busy class goes past the per-user limit.
        builder.UseSetting("RateLimits:UserPerMinute", "10000");
        builder.UseSetting("RateLimits:LoginPerMinute", "10000");

        builder.ConfigureServices(services =>
        {
            // EF Core 9+ keeps the UseSqlServer(...) configuration in IDbContextOptionsConfiguration<T>;
            // without removing it both providers are registered and any DB access throws.
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(ConfigureDatabase);

            // Never send real email from tests; record it so tests can assert on it.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

public sealed record SentEmail(string To, string Subject, string Body);

/// <summary>Test double for <see cref="IEmailSender"/>: keeps every message instead of sending it.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => [.. _sent];

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }
}
