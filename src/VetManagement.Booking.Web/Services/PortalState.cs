using Microsoft.JSInterop;
using MudBlazor;
using VetManagement.Contracts.Scheduling;

namespace VetManagement.Booking.Web.Services;

/// <summary>Clinic info (branding, services) and where to send the client back, shared by all pages.</summary>
public class PortalState(BookingApi api, IJSRuntime js)
{
    private Task<PublicBookingInfoDto?>? _info;

    public Task<PublicBookingInfoDto?> GetInfoAsync() => _info ??= api.GetInfoAsync();

    public async Task<TimeZoneInfo> GetClinicTimeZoneAsync()
        => TimeZoneInfo.FindSystemTimeZoneById((await GetInfoAsync())?.TimeZone ?? "America/Santiago");

    public async Task<MudTheme> GetThemeAsync()
    {
        var color = (await GetInfoAsync())?.PrimaryColor ?? "#1565C0";
        return new MudTheme { PaletteLight = new PaletteLight { Primary = color, AppbarBackground = color } };
    }

    /// <summary>
    /// Remembers the clinic website to return to, only if its origin is one the clinic allows
    /// (otherwise the portal could be abused to redirect people to arbitrary sites).
    /// </summary>
    public async Task RememberReturnUrlAsync(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
            return;
        var allowed = (await GetInfoAsync())?.AllowedReturnOrigins ?? [];
        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (allowed.Any(a => string.Equals(a.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase)))
            await js.InvokeVoidAsync("bookingPortal.setReturnUrl", uri.ToString());
    }

    public async Task<string?> GetReturnUrlAsync() => await js.InvokeAsync<string?>("bookingPortal.getReturnUrl");
}
