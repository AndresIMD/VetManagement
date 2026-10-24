using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Services;

public class ExamService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    public async Task<List<Exam>> GetAllAsync()
    {
        var exams = await unitOfWork.Exams.GetAllAsync();
        return exams.OrderBy(e => e.Name).ToList();
    }

    public async Task<Exam?> GetByIdAsync(int id)
        => await unitOfWork.Exams.GetByIdAsync(id);

    public async Task<Exam?> GetByIdAsNoTrackingAsync(int id)
        => await unitOfWork.Exams.GetByIdAsNoTrackingAsync(id);

    /// <summary>Inventory products whose name, brand or barcode contains <paramref name="search"/>, to link one to an exam.</summary>
    public async Task<List<Item>> SearchStockItemsAsync(string? search)
        => (await unitOfWork.Items.SearchAsync(searchTerm: search)).OrderBy(i => i.Name).Take(15).ToList();

    /// <summary>Names of the inventory products linked to the exams, by id; a product deleted since is missing.</summary>
    public async Task<Dictionary<int, string>> StockItemNamesAsync(IEnumerable<Exam> exams)
    {
        var ids = exams.Select(e => e.ItemId).OfType<int>().Distinct().ToList();
        return ids.Count == 0 ? [] : (await unitOfWork.Items.FindAsync(i => ids.Contains(i.Id))).ToDictionary(i => i.Id, i => i.Name);
    }

    public async Task AddAsync(Exam exam, string userName)
    {
        await unitOfWork.Exams.AddAsync(exam);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(exam.Id, AuditActionType.Add, JsonSerializer.Serialize(exam), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Exam>(exam.Id, "Add");
        }
    }

    public async Task UpdateAsync(Exam exam, Exam? oldData, string userName)
    {
        unitOfWork.Exams.Update(exam);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(exam.Id, AuditActionType.Edit,
            $"Before: {JsonSerializer.Serialize(oldData)}\nAfter: {JsonSerializer.Serialize(exam)}", userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Exam>(exam.Id, "Update");
        }
    }

    public async Task<bool> DeleteAsync(int id, string userName)
    {
        var exam = await unitOfWork.Exams.GetByIdAsync(id);
        if (exam == null)
            return false;

        var snapshot = JsonSerializer.Serialize(exam);
        unitOfWork.Exams.Remove(exam);
        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Exam>(id, "Delete");
        }

        return true;
    }

    private async Task LogAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(Exam),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }
}
