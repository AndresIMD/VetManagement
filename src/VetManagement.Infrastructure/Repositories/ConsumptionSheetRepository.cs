using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Consumption;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class ConsumptionSheetRepository(AppDbContext context) : Repository<ConsumptionSheet>(context), IConsumptionSheetRepository
{
    public async Task<ConsumptionSheet?> GetWithEntriesAsync(int id)
        => await _context.ConsumptionSheets.Include(s => s.Entries).Include(s => s.Stock).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<ConsumptionSheet>> ListAsync(DateTime? closedSinceUtc)
        => await _context.ConsumptionSheets.AsNoTracking().Include(s => s.Entries).Include(s => s.Stock)
            .Where(s => s.ClosedAtUtc == null || (closedSinceUtc != null && s.ClosedAtUtc >= closedSinceUtc))
            .OrderByDescending(s => s.OpenedAtUtc)
            .ToListAsync();
}
