using System.Text.Json;
using VetManagement.Application.Configuration;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Localization;

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
    IReadOnlyDictionary<string, string> Values, string? Interpretation, string? LotNumber = null);

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
        // A typed patient prints as typed; its provisional pet only links the reports' history.
        if (report.Patient is not null || report.PetId is not int petId)
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

        var petIds = latest.Where(r => r.Patient is null).Select(r => r.PetId).OfType<int>().Distinct().ToList();
        var pets = (await unitOfWork.Pets.FindAsync(p => petIds.Contains(p.Id))).ToDictionary(p => p.Id);
        var ownerIds = pets.Values.Select(p => p.OwnerId).Distinct().ToList();
        var owners = (await unitOfWork.Clients.FindAsync(c => ownerIds.Contains(c.Id))).ToDictionary(c => c.Id);

        // A registered pet that was deleted drops its reports from the list; typed patients are always listed.
        return latest.Where(r => r.Patient is not null || r.PetId is null || pets.ContainsKey(r.PetId.Value))
            .Select(r => r.Patient is null && r.PetId is int id
                ? new ExamReportView(r, pets[id], owners.GetValueOrDefault(pets[id].OwnerId))
                : new ExamReportView(r, null, null))
            .ToList();
    }

    /// <summary>Provisional patients whose name contains <paramref name="search"/>, as typed in their latest report; for reuse in the form.</summary>
    public async Task<List<ReportPatient>> ProvisionalPatientsAsync(string? search)
    {
        // ponytail: filters in memory over the typed reports; a query when a clinic has many thousands.
        var term = search?.Trim() ?? string.Empty;
        return (await unitOfWork.ExamReports.FindAsync(r => r.Patient != null && r.PetId != null))
            .GroupBy(r => r.PetId)
            .Select(g => g.OrderByDescending(r => r.ReceivedOn).ThenByDescending(r => r.Id).First().Patient!)
            .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || $"{p.OwnerName} {p.OwnerLastName}".Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .Take(10)
            .ToList();
    }

    public async Task<ExamReportResultStatus> CreateAsync(ExamReportInput input, string userName)
    {
        var report = new ExamReport { CreatedAtUtc = clock.GetUtcNow().UtcDateTime, CreatedBy = userName };
        if (await FillAsync(report, input, userName) is { } error)
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
        if (await FillAsync(report, input, userName) is { } error)
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

    private async Task<ExamReportResultStatus?> FillAsync(ExamReport report, ExamReportInput input, string userName)
    {
        Species species;
        if (input.PetId is int petId)
        {
            var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(petId);
            if (pet is null)
                return Invalid(Texts.T("The pet doesn't exist."));
            report.PetId = pet.Id;
            report.Patient = null;
            species = pet.Species;
        }
        else if (input.Patient is { } patient && !string.IsNullOrWhiteSpace(patient.Name))
        {
            // PetId is set by LinkProvisionalPetAsync once the report is valid (a correction keeps its provisional pet).
            report.Patient = new ReportPatient
            {
                Name = Clean(patient.Name)!,
                Species = patient.Species,
                Breed = Clean(patient.Breed),
                Sex = patient.Sex,
                Birthdate = patient.Birthdate,
                BirthdateApproximate = patient.Birthdate is not null && patient.BirthdateApproximate,
                OwnerName = Clean(patient.OwnerName),
                OwnerLastName = Clean(patient.OwnerLastName)
            };
            species = patient.Species;
        }
        else
            return Invalid(Texts.T("Choose a registered pet or type the patient's name."));

        var exam = await unitOfWork.Exams.GetByIdAsNoTrackingAsync(input.ExamId);
        if (exam is null)
            return Invalid(Texts.T("The exam doesn't exist."));
        var template = (await settingsService.GetAsync()).Settings.FindTemplate(exam.Id);
        if (template is null)
            return Invalid($"'{exam.Name}' has no report template yet: set it up in the exam catalog.");
        if (input.ReceivedOn == default)
            return Invalid(Texts.T("The reception date is required."));

        var unknown = input.Values.Keys.Where(code => template.FindParameter(code) is null or { IsSection: true }).ToList();
        if (unknown.Count > 0)
            return Invalid(Texts.T("Unknown parameters: {0}.", string.Join(", ", unknown)));
        var values = input.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .ToDictionary(v => v.Key, v => v.Value.Trim(), StringComparer.OrdinalIgnoreCase);
        if (values.Count == 0)
            return Invalid(Texts.T("Enter at least one result."));
        if (values.Values.Any(v => v.Length > 200))
            return Invalid(Texts.T("A result can't be longer than 200 characters."));

        report.ExamId = exam.Id;
        report.Title = template.Title;
        report.Method = string.Join(" - ", new[] { exam.Brand, exam.Machine }.Where(s => !string.IsNullOrWhiteSpace(s)));
        report.ReceivedOn = input.ReceivedOn;
        report.ReferringVet = Clean(input.ReferringVet);
        report.LotNumber = Clean(input.LotNumber);
        report.Sections = new ExamReportSections
        {
            PatientData = template.Sections.PatientData,
            Method = template.Sections.Method,
            Interpretation = template.Sections.Interpretation,
            ReportedBy = template.Sections.ReportedBy,
            DirectionArrows = template.Sections.DirectionArrows,
            PreviousResult = template.Sections.PreviousResult,
            LotNumber = template.Sections.LotNumber,
            CriteriaTable = template.Sections.CriteriaTable
        };
        report.Criteria = template.Sections.CriteriaTable
            ? new CriteriaTable
            {
                Title = template.Criteria.Title,
                Columns = [.. template.Criteria.Columns],
                Rows = template.Criteria.Rows.Select(r => new CriteriaRow { Cells = [.. r.Cells] }).ToList()
            }
            : null;
        report.Results = template.ResultsFor(values, species);
        report.Interpretation = string.IsNullOrWhiteSpace(input.Interpretation)
            ? template.Interpret(report.Results)
            : input.Interpretation.Trim();
        if (report.Patient is not null)
            report.PetId = await LinkProvisionalPetAsync(report.PetId, report.Patient, userName);
        await CopyPreviousAsync(report);
        return null;
    }

    /// <summary>
    /// A typed patient gets a provisional client and pet (hidden from client lists and searches), so its reports share a
    /// history. The same patient typed again (name, species and owner) reuses them; a correction updates the report's own.
    /// </summary>
    private async Task<int> LinkProvisionalPetAsync(int? currentPetId, ReportPatient patient, string userName)
    {
        var ownerName = patient.OwnerName ?? string.Empty;
        var ownerLastName = patient.OwnerLastName ?? string.Empty;
        var pet = currentPetId is int id ? await unitOfWork.Pets.GetProvisionalAsync(id) : null;
        Client? owner;
        if (pet is not null)
            owner = (await unitOfWork.Clients.GetWithProvisionalAsync([pet.OwnerId])).Single();
        else
        {
            var candidates = (await unitOfWork.Pets.FindProvisionalByNameAsync(patient.Name)).Where(p => p.Species == patient.Species).ToList();
            var owners = await unitOfWork.Clients.GetWithProvisionalAsync(candidates.Select(p => p.OwnerId).Distinct().ToList());
            owner = owners.FirstOrDefault(o => o.Name == ownerName && o.LastName == ownerLastName);
            pet = owner is null ? null : candidates.First(p => p.OwnerId == owner.Id);
        }

        var isNew = pet is null || owner is null;
        if (isNew)
        {
            pet = new Pet { IsProvisional = true, Name = patient.Name, Breed = string.Empty };
            owner = new Client { IsProvisional = true, Pets = [pet] };
            await unitOfWork.Clients.AddAsync(owner);
        }
        owner!.Name = ownerName;
        owner.LastName = ownerLastName;
        pet!.Name = patient.Name;
        pet.Species = patient.Species;
        pet.Breed = patient.Breed ?? string.Empty;
        pet.Sex = patient.Sex;
        pet.Birthdate = patient.Birthdate?.ToDateTime(TimeOnly.MinValue) ?? default;
        await unitOfWork.SaveChangesAsync();
        if (isNew)
            await audit.LogAsync(nameof(Client), owner.Id, AuditActionType.Add,
                $"Provisional, from an exam report: {JsonSerializer.Serialize(new { owner.Name, owner.LastName, Pet = new { pet.Id, pet.Name, pet.Species, pet.Breed } })}", userName);
        return pet.Id;
    }

    /// <summary>
    /// Turns the report's provisional patient into a registered client and pet (to be completed in the client screens);
    /// its reports then show the live client data. Returns the client id, or null when the report has no provisional patient.
    /// </summary>
    public async Task<int?> RegisterPatientAsync(int reportId, string userName)
    {
        var report = await unitOfWork.ExamReports.GetByIdAsNoTrackingAsync(reportId);
        if (report is not { Patient: not null, PetId: int petId } || await unitOfWork.Pets.GetProvisionalAsync(petId) is not { } pet)
            return null;
        var owner = (await unitOfWork.Clients.GetWithProvisionalAsync([pet.OwnerId])).Single();
        owner.IsProvisional = false;
        pet.IsProvisional = false;
        foreach (var linked in await unitOfWork.ExamReports.FindAsync(r => r.PetId == petId))
        {
            var tracked = await unitOfWork.ExamReports.GetByIdAsync(linked.Id);
            tracked!.Patient = null;
        }
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(Client), owner.Id, AuditActionType.Edit, $"Registered from exam report #{reportId} (was provisional), pet #{pet.Id}", userName);
        return owner.Id;
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
        foreach (var line in report.Results.Where(r => r.Kind != ReportParameterKind.Section))
            line.PreviousValue = previous?.Results.FirstOrDefault(r => string.Equals(r.Code, line.Code, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static ExamReportResultStatus Invalid(string error) => new(ExamReportStatus.Invalid, Error: error);

    /// <summary>Typed report data is stored in capitals, as it prints.</summary>
    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();
}
