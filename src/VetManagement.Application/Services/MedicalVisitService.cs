using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Medical;

namespace VetManagement.Application.Services;

public class MedicalVisitService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    public async Task<List<MedicalVisit>> GetAllAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? patientId = null,
        string? responsible = null,
        PaymentStatus? paymentStatus = null,
        string? search = null)
    {
        var allVisits = await unitOfWork.MedicalVisits.GetAllAsync();
        var query = allVisits.AsEnumerable();

        if (from.HasValue)
            query = query.Where(v => v.Date >= from.Value);
        if (to.HasValue)
            query = query.Where(v => v.Date <= to.Value);
        if (patientId.HasValue)
            query = query.Where(v => v.PatientId == patientId.Value);
        if (!string.IsNullOrWhiteSpace(responsible))
            query = query.Where(v => v.Responsible.Contains(responsible, StringComparison.OrdinalIgnoreCase));
        if (paymentStatus.HasValue)
            query = query.Where(v => v.PaymentStatus == paymentStatus.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
               (v.PatientName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                (v.RecordNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                (v.Location?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                (v.BudgetNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            );
        }

        return query.OrderByDescending(v => v.Date).ToList();
    }

    public async Task<MedicalVisit?> GetByIdAsync(int id)
        => await unitOfWork.MedicalVisits.GetByIdAsync(id);

    public async Task<MedicalVisit?> GetByIdAsNoTrackingAsync(int id)
        => await unitOfWork.MedicalVisits.GetByIdAsNoTrackingAsync(id);

    public async Task AddAsync(MedicalVisit visit, string userName)
    {
        if (string.IsNullOrWhiteSpace(visit.Responsible))
            visit.Responsible = userName;

        await unitOfWork.MedicalVisits.AddAsync(visit);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(visit.Id, AuditActionType.Add, JsonSerializer.Serialize(visit), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<MedicalVisit>(visit.Id, "Add");
            await notificationService.NotifyCollectionChangedAsync<MedicalVisit>("Add");
        }
    }

    public async Task UpdateAsync(MedicalVisit visit, string userName)
    {
        unitOfWork.MedicalVisits.Update(visit);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(visit.Id, AuditActionType.Edit, JsonSerializer.Serialize(visit), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<MedicalVisit>(visit.Id, "Update");
        }
    }

    public async Task<bool> DeleteAsync(int id, string userName)
    {
        var visit = await unitOfWork.MedicalVisits.GetByIdAsync(id);
        if (visit == null)
            return false;

        var snapshot = JsonSerializer.Serialize(visit);
        unitOfWork.MedicalVisits.Remove(visit);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<MedicalVisit>(id, "Delete");
            await notificationService.NotifyCollectionChangedAsync<MedicalVisit>("Delete");
        }

        return true;
    }

    private async Task LogAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(MedicalVisit),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }
}
