using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Reports;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>The management summary adds up what the other modules recorded, by clinic-local dates.</summary>
public class ReportsApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
    private readonly HttpClient _employee = factory.CreateClientWithPermissions([.. Permissions.ForRole("Employee")]);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));
    private static DateTime LocalUtc(DateOnly date, int hour) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, 0)), Zone);

    private static Appointment Appointment(DateOnly date, int hour, AppointmentStatus status, AppointmentSource source = AppointmentSource.Staff) => new()
    {
        ServiceCode = "consultation", ResourceCode = "vet-general", OwnerName = "Ana",
        StartUtc = LocalUtc(date, hour), EndUtc = LocalUtc(date, hour).AddMinutes(30), OccupiedUntilUtc = LocalUtc(date, hour).AddMinutes(30),
        Status = status, Source = source, CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task Summary_AddsUpAgendaIncomeStockAndClinicalActivity()
    {
        var today = Today;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Appointments.AddRange(
                Appointment(today, 3, AppointmentStatus.Completed, AppointmentSource.Online),
                Appointment(today, 4, AppointmentStatus.NoShow),
                Appointment(today, 5, AppointmentStatus.Cancelled),
                Appointment(today.AddDays(1), 10, AppointmentStatus.Confirmed),
                Appointment(today.AddDays(5), 10, AppointmentStatus.Confirmed)); // outside the range
            db.Items.Add(new Item("Collar", ItemType.Material, "RPT-1", null, stock: 4, sellPrice: 3000, buyPrice: 1000));
            await db.SaveChangesAsync();
        }

        var saleId = await BillingApiTests.OpenSaleAsync(_admin);
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Other, Description = "Baño", UnitPrice = 20000 }));
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Cash", Amount = 15000 }));

        var report = (await _admin.GetFromJsonAsync<ReportSummaryDto>($"/api/reports/summary?from={today:yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}"))!;

        report.Agenda.Total.Should().Be(4);
        report.Agenda.Cancelled.Should().Be(1);
        report.Agenda.NoShow.Should().Be(1);
        report.Agenda.Online.Should().Be(1);
        report.Agenda.Upcoming.Should().Be(1);
        report.Agenda.NoShowPercent.Should().Be(50, "1 no-show of 2 past appointments");
        report.Agenda.Resources.Single(r => r.Code == "vet-general").BookedMinutes.Should().Be(60, "completed + upcoming, 30 min each");
        report.Agenda.Services.Single(s => s.Code == "consultation").Count.Should().Be(3, "cancelled ones are not counted");

        report.Billing.Sales.Should().Be(1);
        report.Billing.Revenue.Should().Be(20000);
        report.Billing.Collected.Should().Be(15000);
        report.Billing.OpenBalance.Should().Be(5000);
        report.Billing.ByMethod.Should().ContainSingle(m => m.Key == "Cash" && m.Amount == 15000);
        report.Billing.Daily.Should().HaveCount(2);
        report.Billing.Daily.Single(d => d.Date == today).Amount.Should().Be(15000);
        report.Billing.TopItems.Should().ContainSingle(i => i.Description == "Baño" && i.Amount == 20000);

        report.Inventory.CostValue.Should().BeGreaterThanOrEqualTo(4 * 1000);
        report.Inventory.SaleValue.Should().BeGreaterThanOrEqualTo(4 * 3000);
    }

    [Fact]
    public async Task Summary_IsForManagersAndAdmins_AndLimitsTheRange()
    {
        var today = Today;
        (await _employee.GetAsync($"/api/reports/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _admin.GetAsync($"/api/reports/summary?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.GetAsync($"/api/reports/summary?from={today.AddDays(-400):yyyy-MM-dd}&to={today:yyyy-MM-dd}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Permissions.ForRole("Manager").Should().Contain(Permissions.REPORTS.READ);
    }
}
