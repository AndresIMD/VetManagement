using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VetManagement.Shared.Constants;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Configuration;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Models.DTOs;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using DomainItemType = VetManagement.Domain.Enums.ItemType;
using DomainItemSortField = VetManagement.Domain.Enums.ItemSortField;
using DomainSortDirection = VetManagement.Domain.Enums.SortDirection;
using DomainStockAlertFilter = VetManagement.Domain.Enums.StockAlertFilter;

namespace VetManagement.Shared.Services.Api;

public class ItemsApiService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<Item>?> GetAllItemsAsync()
    {
        var items = await httpClient.GetFromJsonAsync<List<ItemDto>>(ApiRouteConstants.ITEMS_BASE, _caseInsensitiveOptions);
        return items?.Select(MapToShared).ToList();
    }

    public async Task<Item?> GetItemByIdAsync(int id)
    {
        var response = await httpClient.GetAsync(string.Format(ApiRouteConstants.ITEMS_BY_ID, id));
        var item = await response.Content.ReadFromJsonAsync<ItemDto>(_caseInsensitiveOptions);
        return item is null ? null : MapToShared(item);
    }

    public async Task<Item?> GetItemByBarcodeAsync(string barcode)
    {
        var response = await httpClient.GetAsync(string.Format(ApiRouteConstants.ITEMS_BY_BARCODE, barcode));
        if (!response.IsSuccessStatusCode)
            return null;
        var item = await response.Content.ReadFromJsonAsync<ItemDto>(_caseInsensitiveOptions);
        return item is null ? null : MapToShared(item);
    }

    public async Task AddItemAsync(Item item)
    {
        var json = JsonSerializer.Serialize(MapToCreateRequest(item), _caseInsensitiveOptions);
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await httpClient.PostAsync(ApiRouteConstants.ITEMS_BASE, content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API error {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
    }

    public async Task UpdateItemAsync(Item item)
    {
        var json = JsonSerializer.Serialize(MapToUpdateRequest(item), _caseInsensitiveOptions);
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await httpClient.PutAsync(string.Format(ApiRouteConstants.ITEMS_BY_ID, item.Id), content);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API error {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
    }

    public async Task DeleteItemAsync(int id)
        => await httpClient.DeleteAsync(string.Format(ApiRouteConstants.ITEMS_BY_ID, id));

    public async Task<PagedResult<InventoryItemDTO>?> GetItemsPagedAsync(string? search, ItemType type, int page, int pageSize, CancellationToken ct = default, string? alert = null, ItemSortField? sortBy = null, SortDirection sortDirection = SortDirection.Ascending)
    {
        var query = new List<string>
        {
            $"search={Uri.EscapeDataString(search ?? string.Empty)}",
            $"type={(type != ItemType.None ? Uri.EscapeDataString(type.ToString()) : string.Empty)}",
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(alert))
            query.Add($"alert={Uri.EscapeDataString(alert)}");
        if (sortBy.HasValue)
            query.Add($"sortBy={sortBy.Value}");
        if (sortDirection != SortDirection.Ascending)
            query.Add($"sortDirection={sortDirection}");

        var url = $"{ApiRouteConstants.ITEMS_PAGED}?{string.Join("&", query)}";
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<PagedResponse<InventoryItemDto>>(stream, _caseInsensitiveOptions, ct);
        return result is null
            ? null
            : new PagedResult<InventoryItemDTO>
            {
                Items = result.Items.Select(MapToSharedListItem).ToList(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
    }

    private static ItemCreateRequest MapToCreateRequest(Item item) => new()
    {
        Name = item.Name,
        Type = (DomainItemType)item.Type,
        Brand = item.Brand,
        Description = item.Description,
        SellPrice = item.SellPrice,
        BuyPrice = item.BuyPrice,
        Barcode = item.Barcode,
        BrandBarcode = item.BrandBarcode,
        LowStockThreshold = item.LowStockThreshold,
        Compound = item is Drug drug ? drug.Compound : null,
        ML = item is Drug drugItem ? drugItem.ML : 0,
        Concentration = item is Drug drugDetails ? drugDetails.Concentration : 0,
        DosageDogMin = item is Drug dogDrug ? dogDrug.DosageDog.Min : 0,
        DosageDogMax = item is Drug dogDrugMax ? dogDrugMax.DosageDog.Max : 0,
        DosageCatMin = item is Drug catDrug ? catDrug.DosageCat.Min : 0,
        DosageCatMax = item is Drug catDrugMax ? catDrugMax.DosageCat.Max : 0
    };

    private static ItemUpdateRequest MapToUpdateRequest(Item item) => new()
    {
        Name = item.Name,
        Brand = item.Brand,
        Description = item.Description,
        SellPrice = item.SellPrice,
        BuyPrice = item.BuyPrice,
        Barcode = item.Barcode,
        BrandBarcode = item.BrandBarcode,
        LowStockThreshold = item.LowStockThreshold,
        Compound = item is Drug drug ? drug.Compound : null,
        ML = item is Drug drugItem ? drugItem.ML : 0,
        Concentration = item is Drug drugDetails ? drugDetails.Concentration : 0,
        DosageDogMin = item is Drug dogDrug ? dogDrug.DosageDog.Min : 0,
        DosageDogMax = item is Drug dogDrugMax ? dogDrugMax.DosageDog.Max : 0,
        DosageCatMin = item is Drug catDrug ? catDrug.DosageCat.Min : 0,
        DosageCatMax = item is Drug catDrugMax ? catDrugMax.DosageCat.Max : 0
    };

    private static ItemDto MapToItemDto(Item item) => new()
    {
        Name = item.Name,
        Type = (DomainItemType)item.Type,
        Brand = item.Brand,
        Description = item.Description,
        SellPrice = item.SellPrice,
        BuyPrice = item.BuyPrice,
        Barcode = item.Barcode,
        BrandBarcode = item.BrandBarcode,
        LowStockThreshold = item.LowStockThreshold,
        Compound = item is Drug drug ? drug.Compound : null,
        ML = item is Drug drugItem ? drugItem.ML : 0,
        Concentration = item is Drug drugDetails ? drugDetails.Concentration : 0,
        DosageDogMin = item is Drug dogDrug ? dogDrug.DosageDog.Min : 0,
        DosageDogMax = item is Drug dogDrugMax ? dogDrugMax.DosageDog.Max : 0,
        DosageCatMin = item is Drug catDrug ? catDrug.DosageCat.Min : 0,
        DosageCatMax = item is Drug catDrugMax ? catDrugMax.DosageCat.Max : 0
    };

    private static Item MapToShared(ItemDto item)
    {
        if (item.Type == DomainItemType.Drug)
        {
            return new Drug(
                (ItemType)item.Type,
                item.Name,
                item.Barcode,
                item.Description,
                item.Compound ?? string.Empty,
                item.ML,
                item.Concentration,
                new((float)item.DosageDogMin, (float)item.DosageDogMax),
                new((float)item.DosageCatMin, (float)item.DosageCatMax),
                item.Stock,
                item.SellPrice,
                item.Brand,
                item.BrandBarcode)
            {
                Id = item.Id,
                BuyPrice = item.BuyPrice,
                LowStockThreshold = item.LowStockThreshold
            };
        }

        return new Item(
            item.Name,
            (ItemType)item.Type,
            item.Barcode,
            item.Description,
            item.Stock,
            item.SellPrice,
            item.Brand,
            item.Id,
            item.BrandBarcode,
            item.LowStockThreshold,
            item.BuyPrice);
    }

    private static InventoryItemDTO MapToSharedListItem(InventoryItemDto item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Brand = item.Brand,
        Compound = item.Compound,
        Type = (ItemType)item.Type,
        Stock = item.Stock,
        SellPrice = item.SellPrice,
        Barcode = item.Barcode,
        BrandBarcode = item.BrandBarcode,
        LowStockThreshold = item.LowStockThreshold
    };
}
