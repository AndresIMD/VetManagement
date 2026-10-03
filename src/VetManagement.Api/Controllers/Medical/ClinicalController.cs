using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Clinical;
using VetManagement.Application.Configuration;
using VetManagement.Contracts.Clinical;
using VetManagement.Domain.Clinical;

namespace VetManagement.Api.Controllers.Medical;

[Route("api/clinical")]
[Authorize(Policy = "Medical.Read")]
public class ClinicalController(ClinicalService service, ClinicalSettingsService settingsService) : ApiControllerBase
{
    /// <summary>The pet's clinical file: visits and preventive doses with their status.</summary>
    [HttpGet("pets/{petId:int}/history")]
    public async Task<ActionResult<PetHistoryDto>> GetHistoryAsync(int petId)
    {
        var history = await service.GetHistoryAsync(petId);
        if (history is null)
            return NotFound();

        return Ok(new PetHistoryDto
        {
            PetId = history.Pet.Id,
            PetName = history.Pet.Name,
            Species = history.Pet.Species,
            Breed = history.Pet.Breed,
            Sex = history.Pet.Sex,
            Birthdate = history.Pet.Birthdate,
            Weight = history.Pet.Weight,
            OwnerId = history.Pet.OwnerId,
            OwnerName = history.Owner is null ? null : $"{history.Owner.Name} {history.Owner.LastName}".Trim(),
            OwnerEmail = history.Owner?.Email,
            OwnerPhone = history.Owner is { PhoneNumber: > 0 } o ? o.PhoneNumber.ToString() : null,
            Visits = history.Visits.Select(MedicalVisitsController.MapToDto).ToList(),
            Doses = history.Doses.Select(d => MapDose(d.Dose, d.Status)).ToList(),
            Supplies = history.Supplies.Select(s => new VisitSupplyDto
            {
                Id = s.Id, VisitId = s.VisitId, ItemId = s.ItemId, ItemName = s.ItemName, Quantity = s.Quantity,
                Notes = s.Notes, StockDeducted = s.StockDeducted, CreatedBy = s.CreatedBy
            }).ToList()
        });
    }

    [HttpPost("pets/{petId:int}/doses")]
    [Authorize(Policy = "Medical.Create")]
    public async Task<IActionResult> RecordDoseAsync(int petId, [FromBody] RecordDoseRequest request)
        => ToActionResult(await service.RecordDoseAsync(petId, new NewDose(
            request.ProtocolCode, request.ProductName, request.Kind, request.AppliedOn, request.NextDueOn,
            request.BatchNumber, request.VisitId, request.Notes, request.ItemId), GetUserName()));

    [HttpDelete("doses/{id:int}")]
    [Authorize(Policy = "Medical.Delete")]
    public async Task<IActionResult> DeleteDoseAsync(int id)
        => ToActionResult(await service.DeleteDoseAsync(id, GetUserName()));

    /// <summary>Records a drug or material used in the visit (leaves stock when the clinic enables it).</summary>
    [HttpPost("visits/{visitId:int}/supplies")]
    [Authorize(Policy = "Medical.Create")]
    public async Task<IActionResult> AddSupplyAsync(int visitId, [FromBody] AddVisitSupplyRequest request)
        => ToActionResult(await service.AddSupplyAsync(visitId, request.ItemId, request.Quantity, request.Notes, GetUserName()));

    /// <summary>Removes a supply recorded by mistake; its quantity goes back to stock.</summary>
    [HttpDelete("supplies/{id:int}")]
    [Authorize(Policy = "Medical.Delete")]
    public async Task<IActionResult> RemoveSupplyAsync(int id)
        => ToActionResult(await service.RemoveSupplyAsync(id, GetUserName()));

    /// <summary>Pets whose next dose is due within <paramref name="days"/> days, overdue ones first.</summary>
    [HttpGet("due")]
    public async Task<ActionResult<List<DueDoseDto>>> GetDueAsync([FromQuery] int days = 30)
    {
        if (days is < 0 or > 365)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["days"] = ["Use 0 to 365 days."] }));

        return Ok((await service.GetDueAsync(days)).Select(d => new DueDoseDto
        {
            Dose = MapDose(d.Dose, d.Status),
            PetName = d.Pet.Name,
            OwnerName = d.Owner is null ? null : $"{d.Owner.Name} {d.Owner.LastName}".Trim(),
            OwnerEmail = d.Owner?.Email,
            OwnerPhone = d.Owner is { PhoneNumber: > 0 } o ? o.PhoneNumber.ToString() : null
        }).ToList());
    }

    [HttpGet("settings")]
    public async Task<ActionResult<ClinicalSettingsDocument>> GetSettingsAsync()
    {
        var current = await settingsService.GetAsync();
        return Ok(new ClinicalSettingsDocument { Version = current.Version, Settings = JsonSerializer.SerializeToElement(current.Settings, SettingsJson.Options) });
    }

    /// <summary>Saves the whole document. 400 lists every validation error; 409 means it changed since it was read.</summary>
    [HttpPut("settings")]
    [Authorize(Policy = "Clinical.Manage")]
    public async Task<IActionResult> UpdateSettingsAsync([FromBody] ClinicalSettingsDocument document)
    {
        ClinicalSettings? settings;
        try
        {
            settings = document.Settings.Deserialize<ClinicalSettings>(SettingsJson.Options);
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

    private IActionResult ToActionResult(ClinicalResult result) => result.Status switch
    {
        ClinicalStatus.Done => Ok(new { id = result.Id }),
        ClinicalStatus.NotFound => NotFound(),
        _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["dose"] = [result.Error ?? "Invalid request."] }))
    };

    private static PreventiveDoseDto MapDose(PreventiveDose d, Domain.Enums.PreventiveStatus status) => new()
    {
        Id = d.Id,
        PetId = d.PetId,
        ProtocolCode = d.ProtocolCode,
        Kind = d.Kind,
        ProductName = d.ProductName,
        BatchNumber = d.BatchNumber,
        AppliedOn = d.AppliedOn,
        NextDueOn = d.NextDueOn,
        Status = status,
        AppliedBy = d.AppliedBy,
        Notes = d.Notes,
        ReminderSentAtUtc = d.ReminderSentAtUtc,
        ItemId = d.ItemId
    };
}
