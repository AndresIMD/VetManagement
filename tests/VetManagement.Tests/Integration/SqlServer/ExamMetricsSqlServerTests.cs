using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Exams;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>The metrics look into each report's results, a JSON column the in-memory provider doesn't have.</summary>
public class ExamMetricsSqlServerTests
{
    [SqlServerFact]
    public async Task TheReportsOutOfRange_AreCountedInTheDatabase()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var day = new DateOnly(2026, 10, 20);
            await ExamReportStockTests.WithDbAsync(factory, async db =>
            {
                db.ExamReports.AddRange(
                    ExamMetricsApiTests.Report(1, "GLUCOSA", day, "ana", abnormal: true),
                    ExamMetricsApiTests.Report(1, "GLUCOSA", day, "ana", kit: "Tira glucosa", kitUnits: 1),
                    ExamMetricsApiTests.Report(1, "GLUCOSA", day.AddDays(-1), "ana"));
                return await db.SaveChangesAsync();
            });
            var supervisor = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.LABORATORY, Permissions.ROLES.SUPERVISOR])]);

            var m = (await supervisor.GetFromJsonAsync<ExamMetricsDto>($"/api/exam-metrics?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}"))!;

            (m.Reports, m.PreviousReports, m.WithAbnormal, m.KitUnits).Should().Be((2, 1, 1, 1));
            m.ByExam.Should().ContainSingle().Which.WithAbnormal.Should().Be(1);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
