using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>The report's criteria table is a JSON column, which the in-memory provider doesn't have.</summary>
public class ExamReportCriteriaSqlServerTests
{
    [SqlServerFact]
    public async Task AReportSavedBeforeTheHeadersSetting_ReadsWithHeadersInTheFirstRow()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var reportId = await ExamReportStockTests.WithDbAsync(factory, async db =>
            {
                var report = new ExamReport
                {
                    ExamId = 1, Title = "CORTISOL", ReceivedOn = new DateOnly(2026, 10, 20), CreatedBy = "test",
                    Patient = new ReportPatient { Name = "Rocky", Species = Species.Dog },
                    Criteria = new CriteriaTable { Headers = CriteriaHeaders.Both, Columns = ["4 h", "8 h"], Rows = [new CriteriaRow { Cells = ["< 1", "< 1"] }] }
                };
                db.ExamReports.Add(report);
                await db.SaveChangesAsync();
                (await db.Database.SqlQuery<string>($"SELECT JSON_VALUE(Criteria, '$.Headers') AS Value FROM ExamReports WHERE Id = {report.Id}").SingleAsync())
                    .Should().Be("2", "Both is stored as its number");
                // As saved before the setting existed: no "Headers" at all.
                await db.Database.ExecuteSqlAsync($"UPDATE ExamReports SET Criteria = JSON_MODIFY(Criteria, '$.Headers', NULL) WHERE Id = {report.Id}");
                return report.Id;
            });

            var criteria = await ExamReportStockTests.WithDbAsync(factory, async db => (await db.ExamReports.AsNoTracking().SingleAsync(r => r.Id == reportId)).Criteria!);

            criteria.Headers.Should().Be(CriteriaHeaders.FirstRow);
            criteria.Columns.Should().Equal("4 h", "8 h");
            criteria.Rows.Single().Cells.Should().Equal("< 1", "< 1");
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
