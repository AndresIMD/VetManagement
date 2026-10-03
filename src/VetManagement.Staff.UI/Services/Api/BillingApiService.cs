using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Contracts.Billing;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

public sealed record PaymentMethodOption(string Code, string Name, bool Enabled, bool IsCash);

public class BillingApiService(HttpClient http)
{
    public async Task<List<SaleDto>> GetSalesAsync(DateOnly from, DateOnly to, SaleStatus? status = null)
    {
        var url = $"{ApiRouteConstants.BILLING_SALES}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (status is not null)
            url += $"&status={status}";
        return await http.GetFromJsonAsync<List<SaleDto>>(url) ?? [];
    }

    public Task<SaleDto?> GetSaleAsync(int id)
        => http.GetFromJsonAsync<SaleDto>(string.Format(ApiRouteConstants.BILLING_SALE_BY_ID, id));

    /// <summary>For an appointment that already has a sale, the 409 response carries that sale's id.</summary>
    public async Task<ApiResult> CreateSaleAsync(CreateSaleRequest request)
    {
        var result = await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.BILLING_SALES, request));
        if (!result.Ok && result.Body is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
            return result with { Id = id.GetInt32() };
        return result;
    }

    public async Task<ApiResult> AddLineAsync(int saleId, AddSaleLineRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.BILLING_SALE_LINES, saleId), request));

    public async Task<ApiResult> RemoveLineAsync(int saleId, int lineId)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync($"{string.Format(ApiRouteConstants.BILLING_SALE_LINES, saleId)}/{lineId}"));

    public async Task<ApiResult> AddPaymentAsync(int saleId, AddSalePaymentRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.BILLING_SALE_PAYMENTS, saleId), request));

    public async Task<ApiResult> VoidAsync(int saleId, string reason)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.BILLING_SALE_VOID, saleId), new VoidSaleRequest { Reason = reason }));

    public Task<DaySummaryDto?> GetDayAsync(DateOnly date)
        => http.GetFromJsonAsync<DaySummaryDto>(string.Format(ApiRouteConstants.BILLING_CASH_DAY, date.ToString("yyyy-MM-dd")));

    public async Task<ApiResult> CloseDayAsync(CloseDayRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.BILLING_CASH_CLOSE, request));

    public Task<BillingSettingsDocument?> GetSettingsAsync()
        => http.GetFromJsonAsync<BillingSettingsDocument>(ApiRouteConstants.BILLING_SETTINGS);

    public async Task<ApiResult> SaveSettingsAsync(BillingSettingsDocument document)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(ApiRouteConstants.BILLING_SETTINGS, document));

    /// <summary>Payment methods from the settings, for the payment form and display names.</summary>
    public async Task<List<PaymentMethodOption>> GetPaymentMethodsAsync()
    {
        var settings = (await GetSettingsAsync())?.Settings;
        if (settings is not { ValueKind: JsonValueKind.Object } s)
            return [];
        return s.GetProperty("paymentMethods").EnumerateArray().Select(m => new PaymentMethodOption(
            m.GetProperty("code").GetString()!,
            m.GetProperty("name").GetString()!,
            m.GetProperty("enabled").GetBoolean(),
            m.GetProperty("isCash").GetBoolean())).ToList();
    }
}
