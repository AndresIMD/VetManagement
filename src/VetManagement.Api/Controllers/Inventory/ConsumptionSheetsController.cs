using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Consumption;
using VetManagement.Contracts.Consumption;
using VetManagement.Domain.Consumption;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Inventory;

/// <summary>Supplies used on each patient during a stay, added up for the charge at discharge.</summary>
[Route("api/consumption-sheets")]
[Authorize(Policy = "Consumption.Read")]
public class ConsumptionSheetsController(ConsumptionService service) : ApiControllerBase
{
    /// <summary>Open sheets, plus the ones closed in the last <paramref name="closedDays"/> days.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ConsumptionSheetSummaryDto>>> ListAsync([FromQuery] int closedDays = 0)
        => Ok((await service.ListAsync(Math.Clamp(closedDays, 0, 366))).Select(s =>
        {
            var summary = ConsumptionSummary.For(s);
            return new ConsumptionSheetSummaryDto(s.Id, s.PetId, s.PatientName, s.OwnerName, s.OpenedAtUtc, s.ClosedAtUtc, s.Entries.Count,
                summary.Total, s.StockPending, Pending(s, summary), [.. summary.Balances.Where(b => b.IsUsedUp).Select(ToDto)]);
        }).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConsumptionSheetDto>> GetAsync(int id)
        => await service.GetAsync(id) is { } sheet ? Ok(ToDto(sheet)) : NotFound();

    [HttpPost]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> OpenAsync([FromBody] OpenConsumptionSheetRequest request)
        => ToActionResult(await service.OpenAsync(request.PetId, request.PatientName, request.OwnerName, request.Notes, GetUserName()));

    [HttpPost("{id:int}/entries")]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> AddEntryAsync(int id, [FromBody] AddConsumptionEntryRequest request)
        => ToActionResult(await service.AddEntryAsync(id, request.ItemId, request.Quantity, request.Notes, GetUserName()));

    [HttpDelete("{id:int}/entries/{entryId:int}")]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> RemoveEntryAsync(int id, int entryId)
        => ToActionResult(await service.RemoveEntryAsync(id, entryId, GetUserName()));

    [HttpPost("{id:int}/budgets")]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> AddBudgetAsync(int id, [FromBody] AddConsumptionBudgetRequest request)
        => ToActionResult(await service.AddBudgetAsync(id, request.ItemId, request.Quantity, request.Notes, GetUserName()));

    [HttpDelete("{id:int}/budgets/{budgetId:int}")]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> RemoveBudgetAsync(int id, int budgetId)
        => ToActionResult(await service.RemoveBudgetAsync(id, budgetId, GetUserName()));

    [HttpPost("{id:int}/items/{itemId:int}/discard-rest")]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> DiscardRestAsync(int id, int itemId)
        => ToActionResult(await service.DiscardRestAsync(id, itemId, GetUserName()));

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = "Consumption.Record")]
    public async Task<IActionResult> CloseAsync(int id)
        => ToActionResult(await service.CloseAsync(id, GetUserName()));

    [HttpPost("{id:int}/reopen")]
    [Authorize(Policy = "Consumption.Manage")]
    public async Task<IActionResult> ReopenAsync(int id)
        => ToActionResult(await service.ReopenAsync(id, GetUserName()));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Consumption.Manage")]
    public async Task<IActionResult> DeleteAsync(int id)
        => ToActionResult(await service.DeleteAsync(id, GetUserName()));

    [HttpGet("pricing")]
    public async Task<ActionResult<List<ConsumptionPricingDto>>> ListPricingsAsync()
        => Ok((await service.ListPricingsAsync()).Select(v => new ConsumptionPricingDto(
            v.Item.Id, v.Item.Name, v.Item.SellPrice, v.Pricing.Charge, v.Pricing.ContainerMl, v.Pricing.PricePerMl, v.Pricing.Controlled,
            v.Pricing.TakenOutBeforehand, v.Pricing.StockUnitMl)).ToList());

    [HttpPut("pricing")]
    [Authorize(Policy = "Consumption.Manage")]
    public async Task<IActionResult> SetPricingAsync([FromBody] ConsumptionPricingRequest request)
        => ToActionResult(await service.SetPricingAsync(request.ItemId, request.Charge, request.ContainerMl, request.PricePerMl, request.Controlled,
            request.TakenOutBeforehand, GetUserName()));

    [HttpDelete("pricing/{itemId:int}")]
    [Authorize(Policy = "Consumption.Manage")]
    public async Task<IActionResult> DeletePricingAsync(int itemId)
        => ToActionResult(await service.DeletePricingAsync(itemId, GetUserName()));

    private static ConsumptionSheetDto ToDto(ConsumptionSheet sheet)
    {
        var summary = ConsumptionSummary.For(sheet);
        return new ConsumptionSheetDto(sheet.Id, sheet.PetId, sheet.PatientName, sheet.OwnerName, sheet.Notes,
            sheet.OpenedAtUtc, sheet.OpenedBy, sheet.ClosedAtUtc, sheet.ClosedBy,
            [.. sheet.Entries.OrderByDescending(e => e.UsedAtUtc).ThenByDescending(e => e.Id)
                .Select(e => new ConsumptionEntryDto(e.Id, e.ItemId, e.ItemName, e.Charge, e.Quantity, e.Discarded, e.Notes, e.UsedAtUtc, e.CreatedBy))],
            [.. summary.Lines.Select(l => new ConsumptionLineDto(l.ItemId, l.ItemName, l.Charge, l.Given, l.Discarded, l.Containers, l.LeftMl,
                l.ContainerMl, l.UnitPrice, l.Total, sheet.PendingOf(l)))],
            summary.Total, sheet.MovesStock,
            [.. sheet.Budgets.OrderByDescending(b => b.ApprovedAtUtc).ThenByDescending(b => b.Id)
                .Select(b => new ConsumptionBudgetDto(b.Id, b.ItemId, b.ItemName, b.Charge, b.Quantity, b.Notes, b.ApprovedAtUtc, b.CreatedBy))],
            [.. summary.Balances.Select(ToDto)]);
    }

    private static ConsumptionBalanceDto ToDto(ConsumptionBalance b)
        => new(b.ItemId, b.ItemName, b.Charge, b.Budgeted, b.Used, b.Left, b.LeftMl, b.IsUsedUp, b.ToRefund);

    private static List<ConsumptionPendingDto> Pending(ConsumptionSheet sheet, ConsumptionSummary summary)
        => [.. summary.Lines.Select(l => new ConsumptionPendingDto(l.ItemName, sheet.PendingOf(l))).Where(p => p.Units > 0)];

    private IActionResult ToActionResult(ConsumptionResult result) => result.Status switch
    {
        ConsumptionStatus.Done => Ok(new { id = result.Id }),
        ConsumptionStatus.NotFound => NotFound(),
        _ => BadRequestMessage(result.Error ?? Texts.T("Invalid request."))
    };
}
