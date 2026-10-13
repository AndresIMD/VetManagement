using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Configuration;
using VetManagement.Application.Exams;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;

namespace VetManagement.Api.Controllers.Exams;

/// <summary>Printable exam reports, their per-exam templates and the clinic's report branding.</summary>
[Route("api/exam-reports")]
[Authorize(Policy = "ExamsPerformed.Read")]
public class ExamReportsController(ExamReportService service, ExamReportSettingsService settingsService) : ApiControllerBase
{
    /// <summary>Reports of a pet, or the clinic's latest 200; newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ExamReportDto>>> ListAsync([FromQuery] int? petId)
        => Ok((await service.ListAsync(petId)).Select(ToDto).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExamReportDto>> GetAsync(int id)
        => await service.GetAsync(id) is { } view ? Ok(ToDto(view)) : NotFound();

    [HttpPost]
    [Authorize(Policy = "ExamsPerformed.Create")]
    public async Task<IActionResult> CreateAsync([FromBody] ExamReportRequest request)
        => ToActionResult(await service.CreateAsync(ToInput(request), GetUserName()));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExamReportRequest request)
        => ToActionResult(await service.UpdateAsync(id, ToInput(request), GetUserName()));

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

    [HttpGet("images/{kind}")]
    public async Task<IActionResult> GetImageAsync(ClinicImageKind kind)
        => await service.GetImageAsync(kind) is { } image ? File(image.Data, image.ContentType) : NotFound();

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
        => new(r.PetId, r.ExamId, r.ReceivedOn, r.ReferringVet, r.Values, r.Interpretation);

    private IActionResult ToActionResult(ExamReportResultStatus result) => result.Status switch
    {
        ExamReportStatus.Done => Ok(new { id = result.Id }),
        ExamReportStatus.NotFound => NotFound(),
        _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["report"] = [result.Error ?? "Invalid request."] }))
    };

    private static ExamReportDto ToDto(ExamReportView view) => new()
    {
        Id = view.Report.Id,
        PetId = view.Report.PetId,
        ExamId = view.Report.ExamId,
        ExamName = view.ExamName,
        Title = view.Report.Title,
        Method = view.Report.Method,
        ReceivedOn = view.Report.ReceivedOn,
        ReferringVet = view.Report.ReferringVet,
        Interpretation = view.Report.Interpretation,
        Sections = new ExamReportSectionsDto
        {
            PatientData = view.Report.Sections.PatientData,
            Method = view.Report.Sections.Method,
            Interpretation = view.Report.Sections.Interpretation,
            ReportedBy = view.Report.Sections.ReportedBy,
            DirectionArrows = view.Report.Sections.DirectionArrows,
            PreviousResult = view.Report.Sections.PreviousResult
        },
        Results = view.Report.Results.Select(r => new ExamReportResultDto
        {
            Code = r.Code, Name = r.Name, Unit = r.Unit, Value = r.Value, Reference = r.Reference, Band = r.Band, Abnormal = r.Abnormal,
            Direction = r.Direction, BarMin = r.BarMin, BarMax = r.BarMax, PreviousValue = r.PreviousValue
        }).ToList(),
        PreviousReceivedOn = view.Report.PreviousReceivedOn,
        CreatedAtUtc = view.Report.CreatedAtUtc,
        CreatedBy = view.Report.CreatedBy,
        PetName = view.Pet.Name,
        Species = view.Pet.Species,
        Breed = view.Pet.Breed,
        Sex = view.Pet.Sex,
        Birthdate = view.Pet.Birthdate > DateTime.MinValue.AddYears(1) ? view.Pet.Birthdate : null,
        OwnerName = view.Owner is null ? null : $"{view.Owner.Name} {view.Owner.LastName}".Trim(),
        OwnerLastName = view.Owner?.LastName
    };
}
