using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Configuration;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Shared.Services.Api;

public class ItemsApiService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<Item>?> GetAllItemsAsync()
        => await httpClient.GetFromJsonAsync<List<Item>>(ApiRouteConstants.ITEMS_BASE, ItemJsonOptions.GetPolymorphicOptions());

    public async Task<Item?> GetItemByIdAsync(int id)
    {
        var response = await httpClient.GetAsync(string.Format(ApiRouteConstants.ITEMS_BY_ID, id));
        return await response.Content.ReadFromJsonAsync<Item>(ItemJsonOptions.GetPolymorphicOptions());
    }

    public async Task<Item?> GetItemByBarcodeAsync(string barcode)
    {
        var response = await httpClient.GetAsync(string.Format(ApiRouteConstants.ITEMS_BY_BARCODE, barcode));
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<Item>(ItemJsonOptions.GetPolymorphicOptions());
    }

    public async Task AddItemAsync(Item item)
    {
        var json = JsonSerializer.Serialize(item, ItemJsonOptions.GetPolymorphicOptions());
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
        var json = JsonSerializer.Serialize(item, ItemJsonOptions.GetPolymorphicOptions());
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
        return await JsonSerializer.DeserializeAsync<PagedResult<InventoryItemDTO>>(stream, _caseInsensitiveOptions, ct);
    }
}
