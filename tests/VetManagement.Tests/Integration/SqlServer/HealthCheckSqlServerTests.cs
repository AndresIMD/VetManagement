using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>
/// An update whose migration failed leaves the new code on the old schema: /healthz must say so, since start-up only
/// logs the failure and goes on.
/// </summary>
public class HealthCheckSqlServerTests
{
    [SqlServerFact]
    public async Task Healthz_IsUnhealthy_WhileTheDatabaseIsBehindThisVersion()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var previous = db.Database.GetMigrations().ToList()[^2];
            await db.GetService<IMigrator>().MigrateAsync(previous);
        }
        try
        {
            var client = factory.CreateClient();

            var behind = await client.GetAsync("/healthz");
            behind.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await behind.Content.ReadAsStringAsync()).Should().Be("Unhealthy");

            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

            var upToDate = await client.GetAsync("/healthz");
            upToDate.StatusCode.Should().Be(HttpStatusCode.OK);
            (await upToDate.Content.ReadAsStringAsync()).Should().Be("Healthy");
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
