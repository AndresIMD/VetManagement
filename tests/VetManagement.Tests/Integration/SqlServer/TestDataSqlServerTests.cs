using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>Sample data fits the real schema (column lengths, constraints) of SQL Server.</summary>
public class TestDataSqlServerTests
{
    [SqlServerFact]
    public async Task EveryGenerator_WorksOnSqlServer()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
            foreach (var key in new[] { "clients", "pets", "exams", "external-labs", "exam-requests", "inventory", "visits", "doses", "appointments", "sales" })
            {
                var response = await admin.PostAsync($"/api/dev/test-data/{key}?count=100", null);
                response.StatusCode.Should().Be(HttpStatusCode.OK, $"{key}: {await response.Content.ReadAsStringAsync()}");
            }
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
