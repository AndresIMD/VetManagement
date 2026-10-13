using System.Text.Json;
using VetManagement.Application.Configuration;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;

namespace VetManagement.Application.Exams;

public class ExamReportSettingsService(IUnitOfWork unitOfWork, AuditService audit)
    : ClinicSettingsService<ExamReportSettings>(unitOfWork, audit)
{
    protected override string SettingsKey => "exam-reports";
    protected override ExamReportSettings CreateDefaults() => new();
    protected override List<string> Validate(ExamReportSettings settings) => settings.Validate();
}

/// <summary>Either a registered pet (<paramref name="PetId"/>) or a patient typed into the report.</summary>
public sealed record ExamReportInput(int? PetId, ReportPatient? Patient, int ExamId, DateOnly ReceivedOn, string? ReferringVet,
    IReadOnlyDictionary<string, string> Values, string? Interpretation);

/// <summary>A report with its registered pet and owner; both null when the patient was typed into the report.</summary>
public sealed record ExamReportView(ExamReport Report, Pet? Pet, Client? Owner, string? ExamName = null);

public enum ExamReportStatus
{
    Done,
    NotFound,
    Invalid
}

public sealed record ExamReportResultStatus(ExamReportStatus Status, int? Id = null, string? Error = null);

/// <summary>
/// Issued exam reports: the values are entered, everything else (names, units, references, flags and the
/// interpretation) comes from the exam's report template in <see cref="ExamReportSettings"/>.
/// </summary>
public class ExamReportService(IUnitOfWork unitOfWork, ExamReportSettingsService settingsService, AuditService audit, TimeProvider clock)
{
    public async Task<ExamReportView?> GetAsync(int id)
    {
        var report = await unitOfWork.ExamReports.GetByIdAsNoTrackingAsync(id);
        if (report is null)
            return null;
        var exam = await unitOfWork.Exams.GetByIdAsNoTrackingAsync(report.ExamId);
        if (report.PetId is not int petId)
            return new ExamReportView(report, null, null, exam?.Name);
        var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(petId);
        if (pet is null)
            return null;
        return new ExamReportView(report, pet, await unitOfWork.Clients.GetByIdAsNoTrackingAsync(pet.OwnerId), exam?.Name);
    }

    /// <summary>Reports of a pet, or the latest 200 of the clinic; newest first.</summary>
    public async Task<List<ExamReportView>> ListAsync(int? petId)
    {
        // ponytail: loads the clinic's reports to keep the newest 200; a paged query when a clinic has many thousands.
        var reports = petId is int id
            ? await unitOfWork.ExamReports.FindAsync(r => r.PetId == id)
            : await unitOfWork.ExamReports.GetAllAsync();
        var latest = reports.OrderByDescending(r => r.ReceivedOn).ThenByDescending(r => r.Id).Take(200).ToList();

        var petIds = latest.Select(r => r.PetId).OfType<int>().Distinct().ToList();
        var pets = (await unitOfWork.Pets.FindAsync(p => petIds.Contains(p.Id))).ToDictionary(p => p.Id);
        var ownerIds = pets.Values.Select(p => p.OwnerId).Distinct().ToList();
        var owners = (await unitOfWork.Clients.FindAsync(c => ownerIds.Contains(c.Id))).ToDictionary(c => c.Id);

        // A registered pet that was deleted drops its reports from the list; typed patients are always listed.
        return latest.Where(r => r.PetId is null || pets.ContainsKey(r.PetId.Value))
            .Select(r => r.PetId is int id
                ? new ExamReportView(r, pets[id], owners.GetValueOrDefault(pets[id].OwnerId))
                : new ExamReportView(r, null, null))
            .ToList();
    }

    public async Task<ExamReportResultStatus> CreateAsync(ExamReportInput input, string userName)
    {
        var report = new ExamReport { CreatedAtUtc = clock.GetUtcNow().UtcDateTime, CreatedBy = userName };
        if (await FillAsync(report, input) is { } error)
            return error;

        await unitOfWork.ExamReports.AddAsync(report);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ExamReport), report.Id, AuditActionType.Add, JsonSerializer.Serialize(report), userName);
        return new ExamReportResultStatus(ExamReportStatus.Done, report.Id);
    }

    /// <summary>Corrects a report: values are re-evaluated against the template as it is now.</summary>
    public async Task<ExamReportResultStatus> UpdateAsync(int id, ExamReportInput input, string userName)
    {
        var report = await unitOfWork.ExamReports.GetByIdAsync(id);
        if (report is null)
            return new ExamReportResultStatus(ExamReportStatus.NotFound);

        var before = JsonSerializer.Serialize(report);
        if (await FillAsync(report, input) is { } error)
            return error;
        report.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        report.UpdatedBy = userName;

        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ExamReport), report.Id, AuditActionType.Edit, $"Before: {before}\nAfter: {JsonSerializer.Serialize(report)}", userName);
        return new ExamReportResultStatus(ExamReportStatus.Done, report.Id);
    }

    public async Task<ExamReportResultStatus> DeleteAsync(int id, string userName)
    {
        var report = await unitOfWork.ExamReports.GetByIdAsync(id);
        if (report is null)
            return new ExamReportResultStatus(ExamReportStatus.NotFound);

        var snapshot = JsonSerializer.Serialize(report);
        unitOfWork.ExamReports.Remove(report);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ExamReport), id, AuditActionType.Delete, snapshot, userName);
        return new ExamReportResultStatus(ExamReportStatus.Done, id);
    }

    public async Task<ClinicImage?> GetImageAsync(ClinicImageKind kind)
        => (await unitOfWork.ClinicImages.FindAsync(i => i.Kind == kind)).FirstOrDefault();

    /// <summary>Replaces the image of that kind (one per kind).</summary>
    public async Task SetImageAsync(ClinicImageKind kind, string contentType, byte[] data, string userName)
    {
        var image = (await unitOfWork.ClinicImages.FindAsync(i => i.Kind == kind)).FirstOrDefault();
        var isNew = image is null;
        image ??= new ClinicImage { Kind = kind };
        image.ContentType = contentType;
        image.Data = data;
        image.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        image.UpdatedBy = userName;
        if (isNew)
            await unitOfWork.ClinicImages.AddAsync(image);
        else
            unitOfWork.ClinicImages.Update(image);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ClinicImage), image.Id, AuditActionType.Edit, $"{kind}: {contentType}, {data.Length} bytes", userName);
    }

    public async Task<bool> DeleteImageAsync(ClinicImageKind kind, string userName)
    {
        var image = (await unitOfWork.ClinicImages.FindAsync(i => i.Kind == kind)).FirstOrDefault();
        if (image is null)
            return false;
        unitOfWork.ClinicImages.Remove(image);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ClinicImage), image.Id, AuditActionType.Delete, kind.ToString(), userName);
        return true;
    }

    private async Task<ExamReportResultStatus?> FillAsync(ExamReport report, ExamReportInput input)
    {
        Species species;
        if (input.PetId is int petId)
        {
            var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(petId);
            if (pet is null)
                return Invalid("The pet doesn't exist.");
            report.PetId = pet.Id;
            report.Patient = null;
            species = pet.Species;
        }
        else if (input.Patient is { } patient && !string.IsNullOrWhiteSpace(patient.Name))
        {
            report.PetId = null;
            report.Patient = new ReportPatient
            {
                Name = patient.Name.Trim(),
                Species = patient.Species,
                Breed = Clean(patient.Breed),
                Sex = patient.Sex,
                Birthdate = patient.Birthdate,
                OwnerName = Clean(patient.OwnerName),
                OwnerLastName = Clean(patient.OwnerLastName)
            };
            species = patient.Species;
        }
        else
            return Invalid("Choose a registered pet or type the patient's name.");

        var exam = await unitOfWork.Exams.GetByIdAsNoTrackingAsync(input.ExamId);
        if (exam is null)
            return Invalid("The exam doesn't exist.");
        var template = (await settingsService.GetAsync()).Settings.FindTemplate(exam.Id);
        if (template is null)
            return Invalid($"'{exam.Name}' has no report template yet: set it up in the exam catalog.");
        if (input.ReceivedOn == default)
            return Invalid("The reception date is required.");

        var unknown = input.Values.Keys.Where(code => template.FindParameter(code) is null).ToList();
        if (unknown.Count > 0)
            return Invalid($"Unknown parameters: {string.Join(", ", unknown)}.");
        var values = input.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .ToDictionary(v => v.Key, v => v.Value.Trim(), StringComparer.OrdinalIgnoreCase);
        if (values.Count == 0)
            return Invalid("Enter at least one result.");
        if (values.Values.Any(v => v.Length > 200))
            return Invalid("A result can't be longer than 200 characters.");

        report.ExamId = exam.Id;
        report.Title = template.Title;
        report.Method = string.Join(" - ", new[] { exam.Brand, exam.Machine }.Where(s => !string.IsNullOrWhiteSpace(s)));
        report.ReceivedOn = input.ReceivedOn;
        report.ReferringVet = Clean(input.ReferringVet);
        report.Sections = new ExamReportSections
        {
            PatientData = template.Sections.PatientData,
            Method = template.Sections.Method,
            Interpretation = template.Sections.Interpretation,
            ReportedBy = template.Sections.ReportedBy,
            DirectionArrows = template.Sections.DirectionArrows,
            PreviousResult = template.Sections.PreviousResult
        };
        report.Results = template.Parameters
            .Where(p => values.ContainsKey(p.Code))
            .Select(p => p.ToResult(values[p.Code], species))
            .ToList();
        report.Interpretation = string.IsNullOrWhiteSpace(input.Interpretation)
            ? template.Interpret(report.Results)
            : input.Interpretation.Trim();
        await CopyPreviousAsync(report);
        return null;
    }

    /// <summary>
    /// Copies the values of the pet's latest earlier report of the same exam, so a reprint never changes when that
    /// report is corrected later. Typed patients have no history to compare with.
    /// </summary>
    private async Task CopyPreviousAsync(ExamReport report)
    {
        var previous = report.Sections.PreviousResult && report.PetId is not null
            ? (await unitOfWork.ExamReports.FindAsync(r => r.PetId == report.PetId && r.ExamId == report.ExamId
                                                          && r.ReceivedOn < report.ReceivedOn && r.Id != report.Id))
                .OrderByDescending(r => r.ReceivedOn).ThenByDescending(r => r.Id).FirstOrDefault()
            : null;
        report.PreviousReceivedOn = previous?.ReceivedOn;
        foreach (var line in report.Results)
            line.PreviousValue = previous?.Results.FirstOrDefault(r => string.Equals(r.Code, line.Code, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static ExamReportResultStatus Invalid(string error) => new(ExamReportStatus.Invalid, Error: error);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
