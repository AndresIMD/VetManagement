using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using VetManagement.Staff.UI.Services.Api;
using VetManagement.Staff.UI.Services.App;
using VetManagement.Staff.UI.Services.Auth;
using VetManagement.Staff.UI.Services.Realtime;
using VetManagement.Staff.Web;
using AppBackgroundService = VetManagement.Staff.UI.Services.App.BackgroundService;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// UI framework (MudBlazor)
builder.Services.AddMudServices();

// App infrastructure services
builder.Services.AddScoped<AppStatusService>();

// Helpers to sanitize config values
static bool IsPlaceholder(string? value)
 => !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("<") && value.Trim().EndsWith(">");

static string? Normalize(string? value)
 => string.IsNullOrWhiteSpace(value) || IsPlaceholder(value) ? null : value.Trim();

// Load API base URL from wwwroot/config.json; prefer Local if health check succeeds
string? apiBaseUrl = null;
try
{
    using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

    // Avoid browser/service worker caching when fetching config.json
    var cacheBuster = $"?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    var configJson = await http.GetStringAsync($"config.json{cacheBuster}");
    var config = JsonSerializer.Deserialize<ConfigModel>(configJson) ?? new ConfigModel();

    // Optional health probe to decide between Local and Cloud
    async Task<bool> IsHealthyAsync(string baseUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return false;
            using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            var health = new Uri(new Uri(baseUrl), "/healthz");
            using var resp = await probe.GetAsync(health);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    var local = Normalize(config.LocalApiBaseUrl);
    var cloud = Normalize(config.CloudApiBaseUrl);
    var single = Normalize(config.ApiBaseUrl);

    if (!string.IsNullOrWhiteSpace(local) && !string.IsNullOrWhiteSpace(cloud))
    {
        // If both are provided, pick the healthy one
        apiBaseUrl = await IsHealthyAsync(local!) ? local! : cloud!;
    }
    else if (!string.IsNullOrWhiteSpace(local))
    {
        // Only Local provided
        apiBaseUrl = local;
    }
    else if (!string.IsNullOrWhiteSpace(cloud))
    {
        // Only Cloud provided
        apiBaseUrl = cloud;
    }
    else if (!string.IsNullOrWhiteSpace(single))
    {
        // Single ApiBaseUrl provided
        apiBaseUrl = single!;
    }
}
catch
{
    // Ignore read errors; UI will surface a bootstrap state via AppBootstrapError
}

// Always register AppBootstrapError so DI can resolve it in layouts/components
builder.Services.AddSingleton(new VetManagement.Staff.UI.Models.Configuration.AppBootstrapError(
 string.IsNullOrWhiteSpace(apiBaseUrl) ? "Configuration error" : string.Empty,
 string.IsNullOrWhiteSpace(apiBaseUrl) ? "config.json is missing valid API base URLs. Set 'LocalApiBaseUrl' (e.g., https://localhost:44395) or 'ApiBaseUrl'." : string.Empty
));

// Authentication + Authorization (client-side)
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
builder.Services.AddStaffAuthorization();

// Message handler that attaches the JWT to protected API calls
builder.Services.AddTransient<JwtAuthorizationMessageHandler>();


// SignalR Realtime service
builder.Services.AddScoped(sp =>
{
    var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
    return new RealtimeService(
        apiBaseUrl ?? string.Empty,
        async () =>
{
    if (authStateProvider is JwtAuthenticationStateProvider jwtProvider)
        return await jwtProvider.GetTokenAsync();
    return null;
}
    );
});

// Helper to register typed HttpClient services with optional base address and JWT handler
void ConfigureHttpClient<TService>() where TService : class
{
    if (!string.IsNullOrWhiteSpace(apiBaseUrl))
    {
        builder.Services.AddHttpClient<TService>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        }).AddHttpMessageHandler<JwtAuthorizationMessageHandler>();
    }
    else
    {
        // No base URL: the service may handle it internally or fail gracefully
        builder.Services.AddHttpClient<TService>();
    }
}

// Typed API services (protected with JWT)
ConfigureHttpClient<ClientPetApiService>();
ConfigureHttpClient<InventoryMovementApiService>();
ConfigureHttpClient<ExamApiService>();
ConfigureHttpClient<ExamPerformedApiService>();
ConfigureHttpClient<ExternalLabsApiService>();
ConfigureHttpClient<AuditApiService>();
ConfigureHttpClient<UserManagementApiService>();
ConfigureHttpClient<MedicalVisitApiService>();
ConfigureHttpClient<ItemsApiService>();
ConfigureHttpClient<AlertsApiService>();
ConfigureHttpClient<InventoryAlertsApiService>();

// Account API service: WITHOUT JWT handler (used for login/refresh)
builder.Services.AddHttpClient<AccountApiService>(client =>
{
    if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        client.BaseAddress = new Uri(apiBaseUrl);
});

// Background + Theme services
builder.Services.AddScoped<AppBackgroundService>();
builder.Services.AddScoped<ThemeService>();

await builder.Build().RunAsync();

namespace VetManagement.Staff.Web
{
    public class ConfigModel
    {
        public string? ApiBaseUrl { get; set; }
        public string? LocalApiBaseUrl { get; set; }
        public string? CloudApiBaseUrl { get; set; }
    }
}
