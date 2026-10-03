using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Reports;
using VetManagement.Contracts.Reports;

namespace VetManagement.Api.Controllers.Reports;

[Route("api/reports")]
[Authorize(Policy = "Reports.Read")]
public class ReportsController(ReportsService service) : ApiControllerBase
{
    /// <summary>Agenda, income, stock and clinical figures between two clinic-local dates (inclusive).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> GetSummaryAsync([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber >= ReportsService.MaxDays)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["to"] = [$"Use a range of at most {ReportsService.MaxDays} days, ending on or after the start."]
            }));

        var r = await service.GetSummaryAsync(from, to);
        return Ok(new ReportSummaryDto(r.From, r.To,
            new AgendaReportDto(r.Agenda.Total, r.Agenda.Attended, r.Agenda.Upcoming, r.Agenda.Cancelled, r.Agenda.NoShow,
                r.Agenda.NoShowPercent, r.Agenda.Online, r.Agenda.Overbooked,
                r.Agenda.Resources.Select(x => new ResourceOccupancyDto(x.Code, x.Name, x.ScheduledMinutes, x.BookedMinutes, x.OccupancyPercent)).ToList(),
                r.Agenda.Services.Select(x => new NamedCountDto(x.Code, x.Name, x.Count)).ToList()),
            new BillingReportDto(r.Billing.Sales, r.Billing.Voided, r.Billing.Revenue, r.Billing.Collected, r.Billing.AverageTicket, r.Billing.OpenBalance,
                r.Billing.ByMethod.Select(x => new MoneyByKeyDto(x.Key, x.Name, x.Amount)).ToList(),
                r.Billing.Daily.Select(x => new DailyAmountDto(x.Date, x.Amount)).ToList(),
                r.Billing.TopItems.Select(x => new SoldItemDto(x.Description, x.Kind, x.Quantity, x.Amount)).ToList()),
            new InventoryReportDto(r.Inventory.Items, r.Inventory.Units, r.Inventory.CostValue, r.Inventory.SaleValue, r.Inventory.LowStock, r.Inventory.OutOfStock),
            new ClinicalReportDto(r.Clinical.Visits, r.Clinical.DosesApplied, r.Clinical.DosesDueSoon, r.Clinical.DosesOverdue)));
    }
}
