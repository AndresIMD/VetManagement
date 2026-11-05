using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Consumption;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class ConsumptionSheetRepository(AppDbContext context) : Repository<ConsumptionSheet>(context), IConsumptionSheetRepository
{
    /// <summary>A sheet not discharged yet uses the item, or has a budget of it.</summary>
    public async Task<bool> IsOnOpenSheetAsync(int itemId)
        => await _context.ConsumptionSheets.AnyAsync(s => s.ClosedAtUtc == null
            && (s.Entries.Any(e => e.ItemId == itemId) || s.Budgets.Any(b => b.ItemId == itemId)));

    public async Task<ConsumptionSheet?> GetWithEntriesAsync(int id)
        => await _context.ConsumptionSheets.Include(s => s.Entries).Include(s => s.Stock).Include(s => s.Budgets)
            .AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id);

    /// <summary>Open sheets, discharged ones that still owe stock, and those discharged since <paramref name="closedSinceUtc"/>.</summary>
    public async Task<List<ConsumptionSheet>> ListAsync(DateTime? closedSinceUtc)
        => await _context.ConsumptionSheets.AsNoTracking().Include(s => s.Entries).Include(s => s.Stock).Include(s => s.Budgets).AsSplitQuery()
            .Where(s => s.ClosedAtUtc == null || s.StockPending > 0 || (closedSinceUtc != null && s.ClosedAtUtc >= closedSinceUtc))
            .OrderByDescending(s => s.OpenedAtUtc)
            .ToListAsync();
}
