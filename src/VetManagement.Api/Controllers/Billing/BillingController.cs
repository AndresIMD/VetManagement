using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Authorization;
using VetManagement.Application.Billing;
using VetManagement.Application.Configuration;
using VetManagement.Contracts.Billing;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Controllers.Billing;

[Route("api/billing")]
[Authorize(Policy = "Billing.Read")]
public class BillingController(BillingService service, BillingSettingsService settingsService) : ApiControllerBase
{
    [HttpGet("sales")]
    public async Task<ActionResult<List<SaleDto>>> ListAsync([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] SaleStatus? status)
    {
        var today = await service.TodayAsync();
        var (start, end) = (from ?? today, to ?? from ?? today);
        if (end < start || end.DayNumber - start.DayNumber > 92)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["to"] = ["Use a range of at most 92 days."] }));

        var rate = await TaxRateAsync();
        return Ok((await service.ListAsync(start, end, status)).Select(s => MapToDto(s, rate)).ToList());
    }

    [HttpGet("sales/{id:int}")]
    public async Task<ActionResult<SaleDto>> GetAsync(int id)
        => await service.GetAsync(id) is { } sale ? Ok(MapToDto(sale, await TaxRateAsync())) : NotFound();

    /// <summary>What charging the visit would include (appointment service, procedures, supplies), or its existing sales.</summary>
    [HttpGet("visits/{visitId:int}/charge")]
    public async Task<ActionResult<VisitChargePreviewDto>> GetVisitChargeAsync(int visitId)
    {
        var preview = await service.GetVisitChargePreviewAsync(visitId);
        return preview is null ? NotFound() : Ok(new VisitChargePreviewDto(preview.VisitId, preview.CustomerName,
            preview.Lines.Select(l => new VisitChargeLineDto(l.Source, l.Kind, l.Description, l.Quantity, l.UnitPrice, l.TaxExempt)).ToList(),
            preview.OnlineDeposit, preview.SplitByTaxDefault, preview.TaxRatePercent, preview.ExistingSales.Select(s => s.Id).ToList()));
    }

    /// <summary>Charges the visit: one sale, or one taxed and one VAT-exempt. 409 with the ids if it was already charged.</summary>
    [HttpPost("visits/{visitId:int}/charge")]
    [Authorize(Policy = "Billing.Charge")]
    public async Task<IActionResult> ChargeVisitAsync(int visitId, [FromBody] ChargeVisitRequest request)
    {
        var result = await service.ChargeVisitAsync(visitId,
            request.Lines.Select(l => new VisitChargeSelection(l.Source, l.UnitPrice, l.TaxExempt)).ToList(), request.SplitByTax, GetUserName());
        return result.Status switch
        {
            BillingStatus.Done => Ok(new ChargeVisitResponse(result.SaleIds)),
            BillingStatus.NotFound => NotFound(),
            BillingStatus.Conflict => Conflict(new ProblemDetails { Title = result.Error, Status = StatusCodes.Status409Conflict, Extensions = { ["saleIds"] = result.SaleIds } }),
            _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["charge"] = [result.Error ?? "Invalid request."] }))
        };
    }

    [HttpPost("sales")]
    [Authorize(Policy = "Billing.Charge")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSaleRequest request)
        => ToActionResult(await service.CreateAsync(
            new NewSale(request.ClientId, request.PetId, request.AppointmentId, request.CustomerName, request.Notes), GetUserName()));

    [HttpPost("sales/{id:int}/lines")]
    [Authorize(Policy = "Billing.Charge")]
    public async Task<IActionResult> AddLineAsync(int id, [FromBody] AddSaleLineRequest request)
        => ToActionResult(await service.AddLineAsync(id,
            new NewSaleLine(request.Kind, request.Description, request.ServiceCode, request.ItemId, request.Quantity, request.UnitPrice, request.Discount, request.TaxExempt),
            canExceedDiscount: User.HasClaim(Permissions.CLAIM_TYPE, Permissions.BILLING.MANAGE),
            GetUserName()));

    [HttpDelete("sales/{id:int}/lines/{lineId:int}")]
    [Authorize(Policy = "Billing.Charge")]
    public async Task<IActionResult> RemoveLineAsync(int id, int lineId)
        => ToActionResult(await service.RemoveLineAsync(id, lineId, GetUserName()));

    [HttpPost("sales/{id:int}/payments")]
    [Authorize(Policy = "Billing.Charge")]
    public async Task<IActionResult> AddPaymentAsync(int id, [FromBody] AddSalePaymentRequest request)
        => ToActionResult(await service.AddPaymentAsync(id, request.Method, request.Amount, request.Reference, GetUserName()));

    [HttpPost("sales/{id:int}/void")]
    [Authorize(Policy = "Billing.Manage")]
    public async Task<IActionResult> VoidAsync(int id, [FromBody] VoidSaleRequest request)
        => ToActionResult(await service.VoidAsync(id, request.Reason, GetUserName()));

    [HttpGet("cash/{date}")]
    public async Task<ActionResult<DaySummaryDto>> GetDayAsync(DateOnly date)
    {
        var summary = await service.GetDaySummaryAsync(date);
        return Ok(new DaySummaryDto
        {
            Date = summary.Date,
            Totals = summary.Totals.Select(t => new MethodTotalDto(t.Method, t.Name, t.IsCash, t.Amount, t.Count)).ToList(),
            ExpectedCash = summary.ExpectedCash,
            IsClosed = summary.Close is not null,
            CountedCash = summary.Close?.CountedCash,
            Difference = summary.Close?.Difference,
            ClosedBy = summary.Close?.ClosedBy,
            ClosedAtUtc = summary.Close?.ClosedAtUtc,
            Notes = summary.Close?.Notes
        });
    }

    [HttpPost("cash/close")]
    [Authorize(Policy = "Billing.Charge")]
    public async Task<IActionResult> CloseDayAsync([FromBody] CloseDayRequest request)
        => ToActionResult(await service.CloseDayAsync(request.Date, request.CountedCash, request.Notes, GetUserName()));

    [HttpGet("settings")]
    public async Task<ActionResult<BillingSettingsDocument>> GetSettingsAsync()
    {
        var current = await settingsService.GetAsync();
        return Ok(new BillingSettingsDocument { Version = current.Version, Settings = JsonSerializer.SerializeToElement(current.Settings, SettingsJson.Options) });
    }

    /// <summary>Saves the whole document. 400 lists every validation error; 409 means it changed since it was read.</summary>
    [HttpPut("settings")]
    [Authorize(Policy = "Billing.Manage")]
    public async Task<IActionResult> UpdateSettingsAsync([FromBody] BillingSettingsDocument document)
    {
        BillingSettings? settings;
        try
        {
            settings = document.Settings.Deserialize<BillingSettings>(SettingsJson.Options);
        }
        catch (JsonException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [ex.Message] }));
        }
        if (settings is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = ["Settings are required."] }));

        var result = await settingsService.UpdateAsync(settings, document.Version, GetUserName());
        return result.Status switch
        {
            SettingsUpdateStatus.Saved => Ok(new { version = result.Version }),
            SettingsUpdateStatus.Conflict => Conflict(new ProblemDetails
            {
                Title = "The settings were changed by someone else.",
                Detail = $"Reload the latest version ({result.Version}) and apply your changes again.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [.. result.Errors] }))
        };
    }

    private IActionResult ToActionResult(BillingResult result) => result.Status switch
    {
        BillingStatus.Done => Ok(new { id = result.Id }),
        BillingStatus.NotFound => NotFound(),
        BillingStatus.Conflict => Conflict(new ProblemDetails { Title = result.Error, Status = StatusCodes.Status409Conflict, Extensions = { ["id"] = result.Id } }),
        _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["sale"] = [result.Error ?? "Invalid request."] }))
    };

    private async Task<int> TaxRateAsync() => (await settingsService.GetAsync()).Settings.Tax.RatePercent;

    private static SaleDto MapToDto(Sale s, int taxRate) => new()
    {
        Id = s.Id,
        Status = s.Status,
        ClientId = s.ClientId,
        PetId = s.PetId,
        AppointmentId = s.AppointmentId,
        CustomerName = s.CustomerName,
        Notes = s.Notes,
        BusinessDate = s.BusinessDate,
        CreatedAtUtc = s.CreatedAtUtc,
        CreatedBy = s.CreatedBy,
        PaidAtUtc = s.PaidAtUtc,
        VoidReason = s.VoidReason,
        Total = s.Total,
        PaidAmount = s.PaidAmount,
        Balance = s.Balance,
        VisitId = s.VisitId,
        ExemptTotal = s.ExemptTotal,
        TaxableTotal = s.TaxableTotal,
        TaxableNet = s.TaxBreakdown(taxRate).Net,
        Vat = s.TaxBreakdown(taxRate).Vat,
        TaxRatePercent = taxRate,
        Lines = s.Lines.OrderBy(l => l.Id).Select(l => new SaleLineDto
        {
            Id = l.Id, Kind = l.Kind, Description = l.Description, ServiceCode = l.ServiceCode, ItemId = l.ItemId,
            Quantity = l.Quantity, UnitPrice = l.UnitPrice, Discount = l.Discount, Total = l.Total, TaxExempt = l.TaxExempt
        }).ToList(),
        Payments = s.Payments.OrderBy(p => p.Id).Select(p => new SalePaymentDto
        {
            Id = p.Id, Method = p.Method, Amount = p.Amount, Reference = p.Reference,
            ReceivedAtUtc = p.ReceivedAtUtc, BusinessDate = p.BusinessDate, ReceivedBy = p.ReceivedBy
        }).ToList()
    };
}
