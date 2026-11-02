using VetManagement.Domain.Consumption;

namespace VetManagement.Application.Contracts.Persistence;

public interface IConsumptionSheetRepository : IRepository<ConsumptionSheet>
{
    /// <summary>The sheet with its entries and what it took from stock, tracked for changes.</summary>
    Task<ConsumptionSheet?> GetWithEntriesAsync(int id);

    /// <summary>
    /// Open sheets, discharged ones that still owe stock, and the ones closed since <paramref name="closedSinceUtc"/> when
    /// given; with their entries and what they took from stock.
    /// </summary>
    Task<List<ConsumptionSheet>> ListAsync(DateTime? closedSinceUtc);

    /// <summary>Whether a sheet not discharged yet has a use of the item.</summary>
    Task<bool> IsOnOpenSheetAsync(int itemId);
}
