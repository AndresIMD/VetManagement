using VetManagement.Domain.Consumption;

namespace VetManagement.Application.Contracts.Persistence;

public interface IConsumptionSheetRepository : IRepository<ConsumptionSheet>
{
    /// <summary>The sheet with its entries, tracked for changes.</summary>
    Task<ConsumptionSheet?> GetWithEntriesAsync(int id);

    /// <summary>Open sheets, and the ones closed since <paramref name="closedSinceUtc"/> when given; with their entries.</summary>
    Task<List<ConsumptionSheet>> ListAsync(DateTime? closedSinceUtc);
}
