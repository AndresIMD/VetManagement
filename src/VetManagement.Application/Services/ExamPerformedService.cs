using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Application.Services;

public class ExamPerformedService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    public async Task<List<ExamPerformed>> GetAllAsync()
    {
        var exams = await unitOfWork.ExamPerformed.GetAllAsync();
        return exams.OrderByDescending(e => e.Date).ToList();
    }

    public async Task<ExamPerformed?> GetByIdAsync(int id)
        => await unitOfWork.ExamPerformed.GetByIdAsync(id);

    public async Task<ExamPerformed?> GetByIdAsNoTrackingAsync(int id)
        => await unitOfWork.ExamPerformed.GetByIdAsNoTrackingAsync(id);

    public async Task AddAsync(ExamPerformed exam, string userName)
    {
        await unitOfWork.ExamPerformed.AddAsync(exam);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(exam.Id, AuditActionType.Add, JsonSerializer.Serialize(exam), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<ExamPerformed>(exam.Id, "Add");
            await notificationService.NotifyCollectionChangedAsync<ExamPerformed>("Add");
        }
    }

    public async Task UpdateAsync(ExamPerformed exam, string userName)
    {
        unitOfWork.ExamPerformed.Update(exam);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(exam.Id, AuditActionType.Edit, JsonSerializer.Serialize(exam), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<ExamPerformed>(exam.Id, "Update");
        }
    }

    public async Task<bool> DeleteAsync(int id, string userName)
    {
        var exam = await unitOfWork.ExamPerformed.GetByIdAsync(id);
        if (exam == null)
            return false;

        var snapshot = JsonSerializer.Serialize(exam);
        unitOfWork.ExamPerformed.Remove(exam);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<ExamPerformed>(id, "Delete");
            await notificationService.NotifyCollectionChangedAsync<ExamPerformed>("Delete");
        }

        return true;
    }

    private async Task LogAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(ExamPerformed),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }
}
