using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

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
            var term = searchTerm.Trim().ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(term) ||
                (i.Brand != null && i.Brand.ToLower().Contains(term)) ||
                (i.Barcode != null && i.Barcode.ToLower().Contains(term)));
        }

        return await query.ToListAsync();
    }

    public async Task<(List<Item> Items, int TotalCount)> GetPagedListAsync(
        string? searchTerm,
        ItemType? type,
        int page,
        int pageSize,
        StockAlertFilter? alertFilter = null,
        ItemSortField? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending,
        IReadOnlyCollection<int>? hiddenWarehouseIds = null)
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

        // The stock filters and sort use what the user sees: the total less what the hidden warehouses hold.
        var hidden = hiddenWarehouseIds ?? [];
        var stocks = _context.WarehouseStocks;
        var seen = hidden.Count == 0
            ? query.Select(i => new { Item = i, Stock = i.Stock })
            : query.Select(i => new
            {
                Item = i,
                Stock = i.Stock - stocks.Where(s => s.ItemId == i.Id && hidden.Contains(s.WarehouseId)).Sum(s => s.Quantity)
            });

        if (alertFilter.HasValue && alertFilter.Value != StockAlertFilter.None)
        {
            seen = alertFilter.Value switch
            {
                StockAlertFilter.LowStock => seen.Where(x =>
                    x.Item.LowStockThreshold > 0 && x.Stock > 0 && x.Stock <= x.Item.LowStockThreshold),
                StockAlertFilter.OutOfStock => seen.Where(x =>
                    x.Stock <= 0),
                _ => seen
            };
        }

        var totalCount = await seen.CountAsync();

        // Ties go by id, so paging through equal values never repeats or skips an item.
        var isDescending = sortDirection == SortDirection.Descending;
        seen = sortBy switch
        {
            ItemSortField.Id => isDescending ? seen.OrderByDescending(x => x.Item.Id) : seen.OrderBy(x => x.Item.Id),
            ItemSortField.Name => isDescending ? seen.OrderByDescending(x => x.Item.Name).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ItemSortField.Type => isDescending ? seen.OrderByDescending(x => x.Item.Type).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.Type).ThenBy(x => x.Item.Id),
            ItemSortField.Brand => isDescending ? seen.OrderByDescending(x => x.Item.Brand).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.Brand).ThenBy(x => x.Item.Id),
            ItemSortField.Stock => isDescending ? seen.OrderByDescending(x => x.Stock).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Stock).ThenBy(x => x.Item.Id),
            ItemSortField.SellPrice => isDescending ? seen.OrderByDescending(x => x.Item.SellPrice).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.SellPrice).ThenBy(x => x.Item.Id),
            ItemSortField.BuyPrice => isDescending ? seen.OrderByDescending(x => x.Item.BuyPrice).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.BuyPrice).ThenBy(x => x.Item.Id),
            ItemSortField.LowStockThreshold => isDescending ? seen.OrderByDescending(x => x.Item.LowStockThreshold).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.LowStockThreshold).ThenBy(x => x.Item.Id),
            ItemSortField.Barcode => isDescending ? seen.OrderByDescending(x => x.Item.Barcode).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.Barcode).ThenBy(x => x.Item.Id),
            ItemSortField.BrandBarcode => isDescending ? seen.OrderByDescending(x => x.Item.BrandBarcode).ThenBy(x => x.Item.Id) : seen.OrderBy(x => x.Item.BrandBarcode).ThenBy(x => x.Item.Id),
            _ => seen.OrderBy(x => x.Item.Id)
        };

        var items = await seen
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(x => x.Item)
            .ToListAsync();

        return (items, totalCount);
    }
}
