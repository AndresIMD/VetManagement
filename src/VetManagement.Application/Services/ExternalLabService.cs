using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Application.Services;

public class ExternalLabService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    public async Task<List<ExternalLab>> GetAllAsync()
    {
        var labs = await unitOfWork.ExternalLabs.GetAllAsync();
        return labs.OrderBy(x => x.Name).ToList();
    }

    public async Task<ExternalLab?> GetByIdAsync(int id)
        => await unitOfWork.ExternalLabs.GetByIdAsync(id);

    public async Task<ExternalLab?> GetByIdAsNoTrackingAsync(int id)
        => await unitOfWork.ExternalLabs.GetByIdAsNoTrackingAsync(id);

    public async Task<ExternalLab> AddAsync(ExternalLab lab, string userName)
    {
        await unitOfWork.ExternalLabs.AddAsync(lab);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(lab.Id, AuditActionType.Add, JsonSerializer.Serialize(lab), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<ExternalLab>(lab.Id, "Add");
            await notificationService.NotifyCollectionChangedAsync<ExternalLab>("Add");
        }

        return lab;
    }

    public async Task UpdateAsync(ExternalLab lab, string userName)
    {
        unitOfWork.ExternalLabs.Update(lab);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(lab.Id, AuditActionType.Edit, JsonSerializer.Serialize(lab), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<ExternalLab>(lab.Id, "Update");
        }
    }

    public async Task<bool> DeleteAsync(int id, string userName)
    {
        var lab = await unitOfWork.ExternalLabs.GetByIdAsync(id);
        if (lab == null)
            return false;

        var snapshot = JsonSerializer.Serialize(lab);
        unitOfWork.ExternalLabs.Remove(lab);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<ExternalLab>(id, "Delete");
            await notificationService.NotifyCollectionChangedAsync<ExternalLab>("Delete");
        }

        return true;
    }

    private async Task LogAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(ExternalLab),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }
}
