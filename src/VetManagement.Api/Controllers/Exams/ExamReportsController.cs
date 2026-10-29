using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using VetManagement.Api.Authorization;
using VetManagement.Application.Configuration;
using VetManagement.Application.Exams;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Exams;

/// <summary>Printable exam reports, their per-exam templates and the clinic's report branding.</summary>
[Route("api/exam-reports")]
[Authorize(Policy = "ExamsPerformed.Read")]
public class ExamReportsController(ExamReportService service, ExamReportSettingsService settingsService, EnabledModules modules) : ApiControllerBase
{
    /// <summary>Reports of a pet, or the clinic's latest 200; newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ExamReportDto>>> ListAsync([FromQuery] int? petId)
        => Ok((await service.ListAsync(petId)).Select(ToDto).ToList());

    /// <summary>A report; with the Inventory module on, also the exam's product it can deduct.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExamReportDto>> GetAsync(int id)
        => await service.GetAsync(id, withStock: modules.IsEnabled(AppModule.Inventory)) is { } view ? Ok(ToDto(view)) : NotFound();

    /// <summary>Deducts the exam's inventory product (e.g. its test kit) for this report, once; whoever makes reports can.</summary>
    [HttpPost("{id:int}/stock")]
    [Authorize(Policy = "ExamsPerformed.Create")]
    [RequiresModule(AppModule.Inventory)]
    public async Task<IActionResult> DeductStockAsync(int id)
        => ToActionResult(await service.DeductStockAsync(id, GetUserName()));

    /// <summary>Puts back what the report took from inventory; a correction, like editing the report.</summary>
    [HttpDelete("{id:int}/stock")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    [RequiresModule(AppModule.Inventory)]
    public async Task<IActionResult> ReturnStockAsync(int id)
        => ToActionResult(await service.ReturnStockAsync(id, GetUserName()));

    [HttpPost]
    [Authorize(Policy = "ExamsPerformed.Create")]
    public async Task<IActionResult> CreateAsync([FromBody] ExamReportRequest request)
        => ToActionResult(await service.CreateAsync(ToInput(request), GetUserName()));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExamReportRequest request)
        => ToActionResult(await service.UpdateAsync(id, ToInput(request), GetUserName()));

    /// <summary>"Update format": applies the exam's current template to the report's own data (values, patient and dates stay).</summary>
    [HttpPost("{id:int}/renew")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    public async Task<IActionResult> RenewAsync(int id)
        => ToActionResult(await service.RenewAsync(id, GetUserName()));

    /// <summary>Patients typed into earlier reports, to fill the form again.</summary>
    [HttpGet("provisional-patients")]
    [Authorize(Policy = "ExamsPerformed.Create")]
    public async Task<ActionResult<List<ExamReportPatientDto>>> ProvisionalPatientsAsync([FromQuery] string? search)
        => Ok((await service.ProvisionalPatientsAsync(search)).Select(ToPatientDto).ToList());

    /// <summary>Makes the report's provisional patient a registered client and pet; returns the client id.</summary>
    [HttpPost("{id:int}/register-patient")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    [Authorize(Policy = "ClientsPets.Create")]
    public async Task<IActionResult> RegisterPatientAsync(int id)
        => await service.RegisterPatientAsync(id, GetUserName()) is int clientId ? Ok(new { id = clientId }) : NotFound();

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ExamsPerformed.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
        => ToActionResult(await service.DeleteAsync(id, GetUserName()));

    [HttpGet("settings")]
    public async Task<ActionResult<ExamReportSettingsDocument>> GetSettingsAsync()
    {
        var current = await settingsService.GetAsync();
        return Ok(new ExamReportSettingsDocument { Version = current.Version, Settings = JsonSerializer.SerializeToElement(current.Settings, SettingsJson.Options) });
    }

    /// <summary>Saves the whole document. 400 lists every validation error; 409 means it changed since it was read.</summary>
    [HttpPut("settings")]
    [Authorize(Policy = "Exams.Update")]
    public async Task<IActionResult> UpdateSettingsAsync([FromBody] ExamReportSettingsDocument document)
    {
        ExamReportSettings? settings;
        try
        {
            settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options);
        }
        catch (JsonException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [ex.Message] }));
        }
        if (settings is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [Texts.T("Settings are required.")] }));

        var result = await settingsService.UpdateAsync(settings, document.Version, GetUserName());
        return result.Status switch
        {
            SettingsUpdateStatus.Saved => Ok(new { version = result.Version }),
            SettingsUpdateStatus.Conflict => Conflict(new ProblemDetails
            {
                Title = Texts.T("The settings were changed by someone else."),
                Detail = Texts.T("Reload the latest version ({0}) and apply your changes again.", result.Version),
                Status = StatusCodes.Status409Conflict
            }),
            _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [.. result.Errors] }))
        };
    }

    /// <summary>
    /// The image, revalidated on every use: while it is unchanged the browser gets a 304 and keeps its copy, so the
    /// report pages don't download the logo and background (up to 3 MB each) again.
    /// </summary>
    [HttpGet("images/{kind}")]
    public async Task<IActionResult> GetImageAsync(ClinicImageKind kind)
    {
        if (await service.GetImageAsync(kind) is not { } image)
            return NotFound();
        Response.Headers.CacheControl = "private, no-cache";
        return File(image.Data, image.ContentType, new DateTimeOffset(DateTime.SpecifyKind(image.UpdatedAtUtc, DateTimeKind.Utc)),
            new EntityTagHeaderValue($"\"{image.UpdatedAtUtc.Ticks}\""));
    }

    [HttpPut("images/{kind}")]
    [Authorize(Policy = "Exams.Update")]
    public async Task<IActionResult> SetImageAsync(ClinicImageKind kind, [FromBody] ClinicImageRequest request)
    {
        if (!Enum.IsDefined(kind))
            return NotFound();
        await service.SetImageAsync(kind, request.ContentType, request.Data, GetUserName());
        return Ok();
    }

    [HttpDelete("images/{kind}")]
    [Authorize(Policy = "Exams.Update")]
    public async Task<IActionResult> DeleteImageAsync(ClinicImageKind kind)
        => await service.DeleteImageAsync(kind, GetUserName()) ? Ok() : NotFound();

    private static ExamReportInput ToInput(ExamReportRequest r)
        => new(r.PetId, r.Patient is { } p
                ? new ReportPatient
                {
                    Name = p.Name, Species = p.Species, Breed = p.Breed, Sex = p.Sex, Birthdate = p.Birthdate, BirthdateApproximate = p.BirthdateApproximate,
                    OwnerName = p.OwnerName, OwnerLastName = p.OwnerLastName
                }
                : null,
            r.ExamId, r.ReceivedOn, r.ReferringVet, r.Values, r.Interpretation, r.LotNumber);

    private IActionResult ToActionResult(ExamReportResultStatus result) => result.Status switch
    {
        ExamReportStatus.Done => Ok(new { id = result.Id }),
        ExamReportStatus.NotFound => NotFound(),
        _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["report"] = [result.Error ?? Texts.T("Invalid request.")] }))
    };

    private static ExamReportDto ToDto(ExamReportView view) => new()
    {
        Id = view.Report.Id,
        // A typed patient's provisional pet stays internal: the staff screens see a registered pet only.
        PetId = view.Pet is null ? null : view.Report.PetId,
        CanRegisterPatient = view.Report is { Patient: not null, PetId: not null },
        ExamId = view.Report.ExamId,
        ExamName = view.ExamName,
        Title = view.Report.Title,
        Method = view.Report.Method,
        ReceivedOn = view.Report.ReceivedOn,
        ReferringVet = view.Report.ReferringVet,
        LotNumber = view.Report.LotNumber,
        Interpretation = view.Report.Interpretation,
        Sections = new ExamReportSectionsDto
        {
            PatientData = view.Report.Sections.PatientData,
            Method = view.Report.Sections.Method,
            Interpretation = view.Report.Sections.Interpretation,
            ReportedBy = view.Report.Sections.ReportedBy,
            DirectionArrows = view.Report.Sections.DirectionArrows,
            PreviousResult = view.Report.Sections.PreviousResult,
            LotNumber = view.Report.Sections.LotNumber,
            CriteriaTable = view.Report.Sections.CriteriaTable
        },
        Results = view.Report.Results.Select(r => new ExamReportResultDto
        {
            Code = r.Code, Name = r.Name, Kind = r.Kind, Unit = r.Unit, Value = r.Value, Reference = r.Reference, Band = r.Band, Abnormal = r.Abnormal,
            Direction = r.Direction, BarMin = r.BarMin, BarMax = r.BarMax, PreviousValue = r.PreviousValue
        }).ToList(),
        Criteria = view.Report.Criteria is { } table
            ? new CriteriaTableDto { Title = table.Title, Headers = table.Headers, Columns = table.Columns, Rows = table.Rows.Select(r => r.Cells).ToList() }
            : null,
        PreviousReceivedOn = view.Report.PreviousReceivedOn,
        CreatedAtUtc = view.Report.CreatedAtUtc,
        CreatedBy = view.Report.CreatedBy,
        Patient = view.Report.Patient is { } p ? ToPatientDto(p) : null,
        PetName = view.Pet?.Name ?? view.Report.Patient?.Name ?? string.Empty,
        Species = view.Pet?.Species ?? view.Report.Patient?.Species ?? Species.Unknown,
        Breed = view.Pet is null ? view.Report.Patient?.Breed : view.Pet.Breed,
        Sex = view.Pet?.Sex ?? view.Report.Patient?.Sex ?? default,
        Birthdate = view.Pet is { } pet
            ? (pet.Birthdate > DateTime.MinValue.AddYears(1) ? pet.Birthdate : null)
            : view.Report.Patient?.Birthdate?.ToDateTime(TimeOnly.MinValue),
        OwnerName = view.Owner is { } owner
            ? $"{owner.Name} {owner.LastName}".Trim()
            : NullIfEmpty($"{view.Report.Patient?.OwnerName} {view.Report.Patient?.OwnerLastName}".Trim()),
        OwnerLastName = view.Owner?.LastName ?? view.Report.Patient?.OwnerLastName,
        StockToDeduct = view.StockToDeduct is { } stock
            ? new ExamReportStockDto { ItemName = stock.ItemName, Quantity = stock.Quantity, InStock = stock.InStock }
            : null,
        StockDeducted = view.Report.StockDeducted
            ? new ExamReportStockDto
            {
                ItemName = view.Report.StockItemName ?? string.Empty, Quantity = view.Report.StockQuantity,
                AtUtc = view.Report.StockDeductedAtUtc, By = view.Report.StockDeductedBy
            }
            : null
    };

    private static ExamReportPatientDto ToPatientDto(ReportPatient p) => new()
    {
        Name = p.Name, Species = p.Species, Breed = p.Breed, Sex = p.Sex, Birthdate = p.Birthdate, BirthdateApproximate = p.BirthdateApproximate,
        OwnerName = p.OwnerName, OwnerLastName = p.OwnerLastName
    };

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
}
