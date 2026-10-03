using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VetManagement.Application.Configuration;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Inventory;
using VetManagement.Application.Scheduling;
using VetManagement.Application.Services;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Clinical;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Medical;

namespace VetManagement.Application.Clinical;

/// <summary>The clinic's clinical policy (key "clinical"): preventive protocols and reminders.</summary>
public class ClinicalSettingsService(IUnitOfWork unitOfWork, AuditService audit)
    : ClinicSettingsService<ClinicalSettings>(unitOfWork, audit)
{
    protected override string SettingsKey => "clinical";
    protected override ClinicalSettings CreateDefaults() => new();
    protected override List<string> Validate(ClinicalSettings settings) => settings.Validate();
}

public sealed record NewDose(string? ProtocolCode, string? ProductName, PreventiveKind? Kind, DateOnly AppliedOn,
    DateOnly? NextDueOn, string? BatchNumber, int? VisitId, string? Notes, int? ItemId = null);

public sealed record DoseView(PreventiveDose Dose, PreventiveStatus Status);

public sealed record PetHistory(Pet Pet, Client? Owner, List<MedicalVisit> Visits, List<DoseView> Doses, List<VisitSupply> Supplies,
    List<VetManagement.Domain.Billing.Sale> Charges);

/// <summary>A dose whose next application is due, with whom to contact.</summary>
public sealed record DueDose(PreventiveDose Dose, PreventiveStatus Status, Pet Pet, Client? Owner);

public enum ClinicalStatus
{
    Done,
    NotFound,
    Invalid
}

public sealed record ClinicalResult(ClinicalStatus Status, int? Id = null, string? Error = null);

/// <summary>
/// The pet's clinical file: visits and preventive care (vaccines, deworming) with their next due dates, and the
/// owner reminders before a dose is due. A later dose of the same protocol replaces the earlier one.
/// </summary>
public class ClinicalService(
    IUnitOfWork unitOfWork,
    ClinicalSettingsService clinicalSettings,
    SchedulingSettingsService schedulingSettings,
    AuditService audit,
    IEmailSender email,
    TimeProvider clock,
    ILogger<ClinicalService> logger)
{
    private static readonly CultureInfo Chile = CultureInfo.GetCultureInfo("es-CL");

    /// <summary>Doses due this many days back are still reminded; older ones were missed and stay on the due list only.</summary>
    private const int ReminderLookbackDays = 30;

    public async Task<PetHistory?> GetHistoryAsync(int petId)
    {
        var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(petId);
        if (pet is null)
            return null;

        var owner = await unitOfWork.Clients.GetByIdAsNoTrackingAsync(pet.OwnerId);
        var visits = (await unitOfWork.MedicalVisits.GetFilteredAsync(patientId: petId)).ToList();
        var visitIds = visits.Select(v => v.Id).ToList();
        var supplies = (await unitOfWork.VisitSupplies.FindAsync(s => visitIds.Contains(s.VisitId))).OrderBy(s => s.Id).ToList();
        var doses = (await unitOfWork.PreventiveDoses.FindAsync(d => d.PetId == petId)).ToList();
        var settings = (await clinicalSettings.GetAsync()).Settings;
        var today = await TodayAsync();

        var views = doses
            .OrderByDescending(d => d.AppliedOn).ThenByDescending(d => d.Id)
            .Select(d => new DoseView(d, d.StatusOn(today, settings.Reminders.DaysBefore, IsSuperseded(d, doses))))
            .ToList();
        var charges = visitIds.Count == 0 ? [] : await unitOfWork.Sales.GetActiveByVisitsAsync(visitIds);
        return new PetHistory(pet, owner, visits, views, supplies, charges);
    }

    /// <summary>Records a dose; with a protocol, its name, kind and next due date come from the clinic's settings.</summary>
    public async Task<ClinicalResult> RecordDoseAsync(int petId, NewDose request, string userName)
    {
        if (await unitOfWork.Pets.GetByIdAsNoTrackingAsync(petId) is null)
            return new ClinicalResult(ClinicalStatus.NotFound);

        var settings = (await clinicalSettings.GetAsync()).Settings;
        var today = await TodayAsync();
        if (request.AppliedOn > today)
            return new ClinicalResult(ClinicalStatus.Invalid, Error: "The application date can't be in the future.");

        var item = request.ItemId is int itemId ? await unitOfWork.Items.GetByIdAsNoTrackingAsync(itemId) : null;
        if (request.ItemId is not null && item is null)
            return new ClinicalResult(ClinicalStatus.Invalid, Error: "The inventory item doesn't exist.");

        var dose = new PreventiveDose
        {
            PetId = petId,
            ItemId = item?.Id,
            AppliedOn = request.AppliedOn,
            BatchNumber = request.BatchNumber,
            VisitId = request.VisitId,
            Notes = request.Notes,
            AppliedBy = userName,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };

        if (request.ProtocolCode is { Length: > 0 })
        {
            var protocol = settings.FindProtocol(request.ProtocolCode);
            if (protocol is null)
                return new ClinicalResult(ClinicalStatus.Invalid, Error: $"Unknown protocol '{request.ProtocolCode}'.");
            dose.ProtocolCode = protocol.Code;
            dose.Kind = protocol.Kind;
            dose.ProductName = string.IsNullOrWhiteSpace(request.ProductName) ? protocol.Name : request.ProductName.Trim();
            dose.NextDueOn = request.NextDueOn ?? (protocol.IntervalDays > 0 ? request.AppliedOn.AddDays(protocol.IntervalDays) : null);
        }
        else
        {
            var name = string.IsNullOrWhiteSpace(request.ProductName) ? item?.Name : request.ProductName.Trim();
            if (name is null)
                return new ClinicalResult(ClinicalStatus.Invalid, Error: "Pick a protocol or enter the product name.");
            dose.ProductName = name;
            dose.Kind = request.Kind ?? PreventiveKind.Other;
            dose.NextDueOn = request.NextDueOn;
        }

        if (dose.NextDueOn <= dose.AppliedOn)
            return new ClinicalResult(ClinicalStatus.Invalid, Error: "The next dose must be after the application date.");

        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            if (item is not null && settings.DeductStockOnUse)
            {
                var stock = (await unitOfWork.Items.GetByIdAsync(item.Id))?.Stock ?? 0;
                if (stock < 1)
                    return new ClinicalResult(ClinicalStatus.Invalid, Error: $"'{item.Name}' is out of stock.");
                await Stock.MoveAsync(unitOfWork, item.Id, 1, InventoryMovementType.Egress, $"Dose for pet #{petId}", userName, dose.CreatedAtUtc);
                dose.StockDeducted = true;
            }
            await unitOfWork.PreventiveDoses.AddAsync(dose);
            await unitOfWork.SaveChangesAsync();
            return new ClinicalResult(ClinicalStatus.Done, dose.Id);
        });
        if (result.Status != ClinicalStatus.Done)
            return result;

        await audit.LogAsync(nameof(PreventiveDose), dose.Id, AuditActionType.Add, JsonSerializer.Serialize(request), userName);
        return new ClinicalResult(ClinicalStatus.Done, dose.Id);
    }

    /// <summary>Deletes a dose recorded by mistake; its inventory unit goes back to stock.</summary>
    public async Task<ClinicalResult> DeleteDoseAsync(int id, string userName)
    {
        string? snapshot = null;
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var dose = await unitOfWork.PreventiveDoses.GetByIdAsync(id);
            if (dose is null)
                return new ClinicalResult(ClinicalStatus.NotFound);

            snapshot = JsonSerializer.Serialize(dose);
            if (dose.StockDeducted && dose.ItemId is int itemId)
                await Stock.MoveAsync(unitOfWork, itemId, 1, InventoryMovementType.Ingress, $"Dose #{id} deleted", userName, clock.GetUtcNow().UtcDateTime);
            unitOfWork.PreventiveDoses.Remove(dose);
            await unitOfWork.SaveChangesAsync();
            return new ClinicalResult(ClinicalStatus.Done, id);
        });

        if (result.Status == ClinicalStatus.Done)
            await audit.LogAsync(nameof(PreventiveDose), id, AuditActionType.Delete, snapshot!, userName);
        return result;
    }

    /// <summary>Records a drug or material used in a visit; it leaves stock when the clinic enables it.</summary>
    public async Task<ClinicalResult> AddSupplyAsync(int visitId, int itemId, int quantity, string? notes, string userName)
    {
        if (quantity < 1)
            return new ClinicalResult(ClinicalStatus.Invalid, Error: "Quantity must be at least 1.");
        var settings = (await clinicalSettings.GetAsync()).Settings;
        var now = clock.GetUtcNow().UtcDateTime;

        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            if (await unitOfWork.MedicalVisits.GetByIdAsNoTrackingAsync(visitId) is null)
                return new ClinicalResult(ClinicalStatus.NotFound);
            var item = await unitOfWork.Items.GetByIdAsNoTrackingAsync(itemId);
            if (item is null)
                return new ClinicalResult(ClinicalStatus.Invalid, Error: "The inventory item doesn't exist.");
            if (settings.DeductStockOnUse && quantity > item.Stock)
                return new ClinicalResult(ClinicalStatus.Invalid, Error: $"Only {item.Stock} of '{item.Name}' left in stock.");

            var supply = new VisitSupply
            {
                VisitId = visitId, ItemId = item.Id, ItemName = item.Name, Quantity = quantity, Notes = notes,
                CreatedAtUtc = now, CreatedBy = userName
            };
            if (settings.DeductStockOnUse)
            {
                await Stock.MoveAsync(unitOfWork, item.Id, quantity, InventoryMovementType.Egress, $"Visit #{visitId}", userName, now);
                supply.StockDeducted = true;
            }
            await unitOfWork.VisitSupplies.AddAsync(supply);
            await unitOfWork.SaveChangesAsync();
            return new ClinicalResult(ClinicalStatus.Done, supply.Id);
        });

        if (result.Status == ClinicalStatus.Done)
            await audit.LogAsync(nameof(VisitSupply), result.Id!.Value, AuditActionType.Add, $"Visit #{visitId}: item {itemId} x{quantity}", userName);
        return result;
    }

    /// <summary>Removes a supply recorded by mistake; its quantity goes back to stock.</summary>
    public async Task<ClinicalResult> RemoveSupplyAsync(int supplyId, string userName)
    {
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var supply = await unitOfWork.VisitSupplies.GetByIdAsync(supplyId);
            if (supply is null)
                return new ClinicalResult(ClinicalStatus.NotFound);
            if (supply.StockDeducted)
                await Stock.MoveAsync(unitOfWork, supply.ItemId, supply.Quantity, InventoryMovementType.Ingress,
                    $"Visit #{supply.VisitId} supply removed", userName, clock.GetUtcNow().UtcDateTime);
            unitOfWork.VisitSupplies.Remove(supply);
            await unitOfWork.SaveChangesAsync();
            return new ClinicalResult(ClinicalStatus.Done, supplyId);
        });

        if (result.Status == ClinicalStatus.Done)
            await audit.LogAsync(nameof(VisitSupply), supplyId, AuditActionType.Delete, "Removed", userName);
        return result;
    }

    /// <summary>Latest doses whose next application is due up to <paramref name="days"/> from today, overdue ones included.</summary>
    public async Task<List<DueDose>> GetDueAsync(int days)
    {
        var settings = (await clinicalSettings.GetAsync()).Settings;
        var today = await TodayAsync();
        var due = await GetDueDosesAsync(today.AddDays(days));

        var result = new List<DueDose>();
        foreach (var dose in due)
        {
            var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(dose.PetId);
            if (pet is null)
                continue;
            var owner = await unitOfWork.Clients.GetByIdAsNoTrackingAsync(pet.OwnerId);
            result.Add(new DueDose(dose, dose.StatusOn(today, settings.Reminders.DaysBefore, superseded: false), pet, owner));
        }
        return result.OrderBy(d => d.Dose.NextDueOn).ToList();
    }

    /// <summary>Emails owners whose pet's next dose falls within the reminder window. Each dose is reminded once.</summary>
    public async Task<int> SendDueRemindersAsync()
    {
        var settings = (await clinicalSettings.GetAsync()).Settings;
        if (!settings.Reminders.Enabled)
            return 0;

        var agenda = (await schedulingSettings.GetAsync()).Settings;
        var today = await TodayAsync();
        var candidates = (await GetDueDosesAsync(today.AddDays(settings.Reminders.DaysBefore)))
            .Where(d => d.ReminderSentAtUtc is null && d.NextDueOn >= today.AddDays(-ReminderLookbackDays))
            .ToList();

        var sent = 0;
        foreach (var dose in candidates)
        {
            var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(dose.PetId);
            var owner = pet is null ? null : await unitOfWork.Clients.GetByIdAsNoTrackingAsync(pet.OwnerId);
            if (pet is null || owner is null || string.IsNullOrWhiteSpace(owner.Email))
                continue;

            var contact = string.IsNullOrWhiteSpace(agenda.ClinicPhone) ? "" : $" llamando al {agenda.ClinicPhone}";
            var when = dose.NextDueOn!.Value < today ? "estaba programada para" : "corresponde el";
            var body =
                $"Hola {owner.Name},{Environment.NewLine}{Environment.NewLine}" +
                $"Te recordamos que la próxima dosis de {dose.ProductName} de {pet.Name} {when} " +
                $"{dose.NextDueOn.Value.ToString("dddd d 'de' MMMM 'de' yyyy", Chile)}.{Environment.NewLine}{Environment.NewLine}" +
                $"Agenda tu hora{contact}.{Environment.NewLine}{Environment.NewLine}{agenda.ClinicName}";
            try
            {
                await email.SendAsync(owner.Email, $"Recordatorio: {dose.ProductName} de {pet.Name} – {agenda.ClinicName}", body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not send the preventive reminder for dose {DoseId}", dose.Id);
                continue;
            }

            dose.ReminderSentAtUtc = clock.GetUtcNow().UtcDateTime;
            unitOfWork.PreventiveDoses.Update(dose);
            await unitOfWork.SaveChangesAsync();
            sent++;
        }
        return sent;
    }

    /// <summary>The latest dose of each series with a next due date up to <paramref name="until"/>.</summary>
    private async Task<List<PreventiveDose>> GetDueDosesAsync(DateOnly until)
    {
        var due = (await unitOfWork.PreventiveDoses.FindAsync(d => d.NextDueOn != null && d.NextDueOn <= until)).ToList();
        if (due.Count == 0)
            return due;

        var petIds = due.Select(d => d.PetId).Distinct().ToList();
        var allOfThosePets = (await unitOfWork.PreventiveDoses.FindAsync(d => petIds.Contains(d.PetId))).ToList();
        return due.Where(d => !IsSuperseded(d, allOfThosePets)).ToList();
    }

    private static bool IsSuperseded(PreventiveDose dose, IEnumerable<PreventiveDose> petDoses)
        => petDoses.Any(other => other.PetId == dose.PetId && other.Id != dose.Id && other.SeriesKey == dose.SeriesKey
                                 && (other.AppliedOn > dose.AppliedOn || (other.AppliedOn == dose.AppliedOn && other.Id > dose.Id)));

    private async Task<DateOnly> TodayAsync()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById((await schedulingSettings.GetAsync()).Settings.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, zone));
    }
}
