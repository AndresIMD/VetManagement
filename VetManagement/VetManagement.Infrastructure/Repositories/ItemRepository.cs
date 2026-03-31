using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Infrastructure.Repositories;

public class ItemRepository(AppDbContext context) : Repository<Item>(context), IItemRepository
{
    public async Task<Item?> GetByBarcodeAsync(string barcode)
    {
        return await _context.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Barcode == barcode);
    }

    public async Task<IEnumerable<Item>> GetLowStockAsync()
    {
        return await _context.Items
            .AsNoTracking()
            .Where(i => i.LowStockThreshold > 0 && i.Stock > 0 && i.Stock <= i.LowStockThreshold)
            .ToListAsync();
    }

    public async Task<IEnumerable<Item>> GetOutOfStockAsync()
    {
        return await _context.Items
            .AsNoTracking()
            .Where(i => i.Stock == 0)
            .ToListAsync();
    }

    public async Task<IEnumerable<Item>> SearchAsync(ItemType? type = null, string? searchTerm = null)
    {
        var query = _context.Items.AsNoTracking();

        if (type.HasValue && type.Value != ItemType.None)
        {
            query = query.Where(i => i.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(i =>
                i.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                (i.Brand != null && i.Brand.Contains(term, StringComparison.CurrentCultureIgnoreCase)) ||
                (i.Barcode != null && i.Barcode.Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        }

        return await query.ToListAsync();
    }

    public async Task<(List<Item> Items, int TotalCount)> GetPagedListAsync(
        string? searchTerm,
        ItemType? type,
        int page,
        int pageSize,
        string? alert,
        ItemSortField? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        var query = _context.Items.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(i =>
                i.Name.Contains(term) ||
                (i.Brand != null && i.Brand.Contains(term)) ||
                (i.Barcode != null && i.Barcode.Contains(term)) ||
                (i.BrandBarcode != null && i.BrandBarcode.Contains(term)) ||
                (i is Drug && ((Drug)i).Compound.Contains(term))
            );
        }

        if (type.HasValue && type.Value != ItemType.None)
        {
            query = query.Where(i => i.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(alert))
        {
            var alertLower = alert.ToLowerInvariant();
            if (alertLower == "zero")
                query = query.Where(i => i.Stock == 0);
            else if (alertLower == "low")
                query = query.Where(i => i.LowStockThreshold > 0 && i.Stock > 0 && i.Stock <= i.LowStockThreshold);
        }

        var totalCount = await query.CountAsync();

        var isDescending = sortDirection == SortDirection.Descending;
        query = sortBy switch
        {
            ItemSortField.Name => isDescending ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name),
            ItemSortField.Type => isDescending ? query.OrderByDescending(i => i.Type) : query.OrderBy(i => i.Type),
            ItemSortField.Brand => isDescending ? query.OrderByDescending(i => i.Brand) : query.OrderBy(i => i.Brand),
            ItemSortField.Stock => isDescending ? query.OrderByDescending(i => i.Stock) : query.OrderBy(i => i.Stock),
            ItemSortField.SellPrice => isDescending ? query.OrderByDescending(i => i.SellPrice) : query.OrderBy(i => i.SellPrice),
            ItemSortField.Barcode => isDescending ? query.OrderByDescending(i => i.Barcode) : query.OrderBy(i => i.Barcode),
            _ => query.OrderBy(i => i.Name)
        };

        var items = await query
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
