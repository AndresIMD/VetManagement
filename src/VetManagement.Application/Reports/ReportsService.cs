using VetManagement.Application.Billing;
using VetManagement.Application.Clinical;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Scheduling;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Reports;

public sealed record ResourceOccupancy(string Code, string Name, int ScheduledMinutes, int BookedMinutes)
{
    /// <summary>Booked share of the regular hours (overflow blocks and overbooking can push it over 100).</summary>
    public int OccupancyPercent => ScheduledMinutes == 0 ? 0 : (int)Math.Round(BookedMinutes * 100.0 / ScheduledMinutes);
}

public sealed record NamedCount(string Code, string Name, int Count);

public sealed record AgendaReport(int Total, int Attended, int Upcoming, int Cancelled, int NoShow, int Online, int Overbooked,
    IReadOnlyList<ResourceOccupancy> Resources, IReadOnlyList<NamedCount> Services)
{
    /// <summary>No-shows over appointments whose time has passed (attended + no-show).</summary>
    public int NoShowPercent => Attended + NoShow == 0 ? 0 : (int)Math.Round(NoShow * 100.0 / (Attended + NoShow));
}

public sealed record MoneyByKey(string Key, string Name, int Amount);

public sealed record DailyAmount(DateOnly Date, int Amount);

public sealed record SoldItem(string Description, SaleLineKind Kind, int Quantity, int Amount);

public sealed record BillingReport(int Sales, int Voided, int Revenue, int Collected, int AverageTicket, int OpenBalance,
    IReadOnlyList<MoneyByKey> ByMethod, IReadOnlyList<DailyAmount> Daily, IReadOnlyList<SoldItem> TopItems);

public sealed record InventoryReport(int Items, int Units, long CostValue, long SaleValue, int LowStock, int OutOfStock);

public sealed record ClinicalReport(int Visits, int DosesApplied, int DosesDueSoon, int DosesOverdue);

public sealed record ReportSummary(DateOnly From, DateOnly To, AgendaReport Agenda, BillingReport Billing, InventoryReport Inventory, ClinicalReport Clinical);

/// <summary>
/// Management figures for a period, by the clinic's local dates: agenda use, income, stock value and clinical activity.
/// Read-only; everything comes from the records the other modules already keep.
/// </summary>
public class ReportsService(
    IUnitOfWork unitOfWork,
    SchedulingSettingsService schedulingSettings,
    BillingSettingsService billingSettings,
    ClinicalSettingsService clinicalSettings,
    ClinicalService clinical,
    TimeProvider clock)
{
    public const int MaxDays = 366;

    public async Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to)
    {
        var agenda = (await schedulingSettings.GetAsync()).Settings;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(agenda.TimeZone);
        var (fromUtc, toUtc) = AgendaRules.UtcRange(from, to, zone); // padded a day each side; filtered by local date below
        DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
        bool InRange(DateOnly d) => d >= from && d <= to;

        return new ReportSummary(from, to,
            await AgendaAsync(agenda, from, to, fromUtc, toUtc, LocalDate, InRange),
            await BillingAsync(from, to),
            await InventoryAsync(),
            await ClinicalAsync(from, to, fromUtc, toUtc, LocalDate, InRange));
    }

    private async Task<AgendaReport> AgendaAsync(SchedulingSettings settings, DateOnly from, DateOnly to, DateTime fromUtc, DateTime toUtc,
        Func<DateTime, DateOnly> localDate, Func<DateOnly, bool> inRange)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var appointments = (await unitOfWork.Appointments.GetOverlappingAsync(fromUtc, toUtc))
            .Where(a => inRange(localDate(a.StartUtc)))
            // Online bookings that were never paid didn't really take a slot.
            .Where(a => !(a.Status == AppointmentStatus.PendingPayment && !a.OccupiesSlot(now)))
            .ToList();
        var live = appointments.Where(a => a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow)).ToList();

        var resources = settings.Resources.Where(r => r.Enabled).Select(r =>
        {
            var scheduled = 0;
            for (var day = from; day <= to; day = day.AddDays(1))
                scheduled += AvailabilityCalculator.BlocksFor(settings, r, day)
                    .Where(b => !b.IsOverflow)
                    .Sum(b => (int)(b.End - b.Start).TotalMinutes * Math.Max(b.MaxParallel, 1));
            var booked = live.Where(a => string.Equals(a.ResourceCode, r.Code, StringComparison.OrdinalIgnoreCase))
                .Sum(a => (int)(a.EndUtc - a.StartUtc).TotalMinutes);
            return new ResourceOccupancy(r.Code, r.Name, scheduled, booked);
        }).ToList();

        var services = appointments.Where(a => a.Status != AppointmentStatus.Cancelled)
            .GroupBy(a => a.ServiceCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new NamedCount(g.Key,
                settings.Services.FirstOrDefault(s => string.Equals(s.Code, g.Key, StringComparison.OrdinalIgnoreCase))?.Name ?? g.Key, g.Count()))
            .OrderByDescending(s => s.Count).ToList();

        return new AgendaReport(
            Total: appointments.Count,
            Attended: live.Count(a => a.Status == AppointmentStatus.Completed || (a.Status == AppointmentStatus.Confirmed && a.EndUtc <= now)),
            Upcoming: live.Count(a => a.Status != AppointmentStatus.Completed && a.EndUtc > now),
            Cancelled: appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            NoShow: appointments.Count(a => a.Status == AppointmentStatus.NoShow),
            Online: appointments.Count(a => a.Source == AppointmentSource.Online),
            Overbooked: appointments.Count(a => a.IsOverbooked),
            resources, services);
    }

    private async Task<BillingReport> BillingAsync(DateOnly from, DateOnly to)
    {
        var settings = (await billingSettings.GetAsync()).Settings;
        var sales = await unitOfWork.Sales.ListAsync(from, to, status: null);
        var valid = sales.Where(s => s.Status != SaleStatus.Voided).ToList();
        var payments = await unitOfWork.Sales.GetPaymentsAsync(from, to);

        var byMethod = payments.GroupBy(p => p.Method, StringComparer.OrdinalIgnoreCase)
            .Select(g => new MoneyByKey(g.Key,
                g.Key == SalePayment.OnlineDepositMethod ? "Abono online" : settings.FindMethod(g.Key)?.Name ?? g.Key, g.Sum(p => p.Amount)))
            .OrderByDescending(m => m.Amount).ToList();

        var daily = new List<DailyAmount>();
        for (var day = from; day <= to; day = day.AddDays(1))
            daily.Add(new DailyAmount(day, payments.Where(p => p.BusinessDate == day).Sum(p => p.Amount)));

        var topItems = valid.SelectMany(s => s.Lines)
            .GroupBy(l => (l.Kind, Key: l.ItemId?.ToString() ?? l.ServiceCode ?? l.Description))
            .Select(g => new SoldItem(g.First().Description, g.Key.Kind, g.Sum(l => l.Quantity), g.Sum(l => l.Total)))
            .OrderByDescending(i => i.Amount).Take(10).ToList();

        var revenue = valid.Sum(s => s.Total);
        return new BillingReport(
            Sales: valid.Count,
            Voided: sales.Count - valid.Count,
            Revenue: revenue,
            Collected: payments.Sum(p => p.Amount),
            AverageTicket: valid.Count == 0 ? 0 : revenue / valid.Count,
            OpenBalance: valid.Where(s => s.Status == SaleStatus.Open).Sum(s => s.Balance),
            byMethod, daily, topItems);
    }

    private async Task<InventoryReport> InventoryAsync()
    {
        var items = await unitOfWork.Items.GetAllAsync();
        var inStock = items.Where(i => i.Stock > 0).ToList();
        return new InventoryReport(
            Items: items.Count,
            Units: inStock.Sum(i => i.Stock),
            CostValue: inStock.Sum(i => (long)i.Stock * i.BuyPrice),
            SaleValue: inStock.Sum(i => (long)i.Stock * i.SellPrice),
            LowStock: items.Count(i => i.Stock > 0 && i.Stock <= i.LowStockThreshold),
            OutOfStock: items.Count(i => i.Stock <= 0));
    }

    private async Task<ClinicalReport> ClinicalAsync(DateOnly from, DateOnly to, DateTime fromUtc, DateTime toUtc,
        Func<DateTime, DateOnly> localDate, Func<DateOnly, bool> inRange)
    {
        var visits = (await unitOfWork.MedicalVisits.FindAsync(v => v.Date >= fromUtc && v.Date < toUtc)).Count(v => inRange(localDate(v.Date)));
        var doses = (await unitOfWork.PreventiveDoses.FindAsync(d => d.AppliedOn >= from && d.AppliedOn <= to)).Count();
        var window = (await clinicalSettings.GetAsync()).Settings.Reminders.DaysBefore;
        var due = await clinical.GetDueAsync(window);
        return new ClinicalReport(visits, doses,
            DosesDueSoon: due.Count(d => d.Status == PreventiveStatus.DueSoon),
            DosesOverdue: due.Count(d => d.Status == PreventiveStatus.Overdue));
    }
}
