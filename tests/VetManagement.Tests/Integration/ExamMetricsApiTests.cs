using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Exams;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>The lab's figures: reports per period by the date the sample was received, per exam and per person.</summary>
public class ExamMetricsApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _supervisor = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.LABORATORY, Permissions.ROLES.SUPERVISOR])]);
    private readonly HttpClient _lab = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.LABORATORY)]);

    /// <summary>A week of its own, far from the other tests' reports.</summary>
    private static readonly DateOnly Monday = new(2031, 3, 3);

    internal static ExamReport Report(int examId, string title, DateOnly receivedOn, string by, bool abnormal = false, string? kit = null, int kitUnits = 0)
        => new()
        {
            ExamId = examId, Title = title, ReceivedOn = receivedOn, CreatedBy = by, CreatedAtUtc = DateTime.UtcNow,
            Patient = new ReportPatient { Name = "Rocky", Species = Species.Dog },
            Results = [new ExamReportResult { Code = "GLU", Name = "Glucosa", Value = abnormal ? "250" : "90", Abnormal = abnormal }],
            StockItemId = kit is null ? null : 1, StockItemName = kit, StockQuantity = kitUnits
        };

    private Task SeedAsync(params ExamReport[] reports) => ExamReportStockTests.WithDbAsync(factory, async (AppDbContext db) =>
    {
        db.ExamReports.AddRange(reports);
        return await db.SaveChangesAsync();
    });

    [Fact]
    public async Task AWeek_CountsItsReportsByDay_ExamAndPerson_WithTheOnesOutOfRangeAndTheKitsUsed()
    {
        var (blood, glucose) = (Random.Shared.Next(100_000, 200_000), Random.Shared.Next(200_000, 300_000));
        await SeedAsync(
            Report(blood, "HEMOGRAMA", Monday, "ana", abnormal: true),
            Report(blood, "HEMOGRAMA", Monday.AddDays(1), "ana", kit: "Kit hemograma", kitUnits: 2),
            Report(blood, "HEMOGRAMA", Monday.AddDays(1), "Ana"),
            Report(glucose, "GLUCOSA", Monday.AddDays(2), "luis", abnormal: true),
            // The week before, to compare with; and one after the period, left out.
            Report(glucose, "GLUCOSA", Monday.AddDays(-5), "luis"),
            Report(glucose, "GLUCOSA", Monday.AddDays(-2), "luis"),
            Report(glucose, "GLUCOSA", Monday.AddDays(9), "luis"));

        var m = (await _supervisor.GetFromJsonAsync<ExamMetricsDto>($"/api/exam-metrics?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}"))!;

        (m.Reports, m.PreviousReports, m.WithAbnormal, m.KitUnits).Should().Be((4, 2, 2, 2));
        m.Grouping.Should().Be(MetricsGrouping.Day);
        m.Buckets.Select(b => b.Reports).Should().Equal(1, 2, 1, 0, 0, 0, 0);
        m.ByExam.Select(e => (e.Name, e.Reports, e.WithAbnormal)).Should().Equal(("HEMOGRAMA", 3, 1), ("GLUCOSA", 1, 1));
        m.ByReporter.Select(r => (r.User.ToLowerInvariant(), r.Reports)).Should().Equal(("ana", 3), ("luis", 1));
        m.Kits.Should().ContainSingle().Which.Should().Be(new KitUseDto("Kit hemograma", 1, 2));
    }

    [Fact]
    public async Task ASingleDay_IsOneBar_AndAnEmptyPeriodCountsNothing()
    {
        var empty = (await _supervisor.GetFromJsonAsync<ExamMetricsDto>("/api/exam-metrics?from=2032-01-07&to=2032-01-07"))!;

        empty.Grouping.Should().Be(MetricsGrouping.Day, "a report has a date, not an hour");
        empty.Buckets.Should().ContainSingle().Which.Reports.Should().Be(0);
        (empty.Reports, empty.WithAbnormal, empty.KitUnits).Should().Be((0, 0, 0));
        empty.ByExam.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyTheLabSupervisor_SeesThem_ForAtMostAYear()
    {
        (await _lab.GetAsync("/api/exam-metrics?from=2031-03-03&to=2031-03-09")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _supervisor.GetAsync("/api/exam-metrics?from=2031-03-09&to=2031-03-03")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _supervisor.GetAsync("/api/exam-metrics?from=2030-01-01&to=2031-03-03")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
