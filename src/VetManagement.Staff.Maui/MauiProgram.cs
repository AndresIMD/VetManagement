using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using System.Text.Json;
using VetManagement.Staff.UI.Services.Api;
using VetManagement.Staff.UI.Services.App;
using VetManagement.Staff.UI.Services.Auth;
using VetManagement.Staff.UI.Services.Realtime;

namespace VetManagement.Staff.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        // Get the API base URL from config.json, prefer Local if healthy
        string apiBaseUrl;
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (!File.Exists(configPath))
                throw new InvalidOperationException($"Configuration file was not found: {configPath}");
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ConfigModel>(configJson) ?? new ConfigModel();

            static bool IsHealthy(string? baseUrl)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(baseUrl))
                        return false;
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
                    var health = new Uri(new Uri(baseUrl), "/healthz");
                    var resp = client.GetAsync(health).GetAwaiter().GetResult();
                    return resp.IsSuccessStatusCode;
                }
                catch { return false; }
            }

            if (!string.IsNullOrWhiteSpace(config.LocalApiBaseUrl) && !string.IsNullOrWhiteSpace(config.CloudApiBaseUrl))
            {
                apiBaseUrl = IsHealthy(config.LocalApiBaseUrl) ? config.LocalApiBaseUrl! : config.CloudApiBaseUrl!;
            }
            else if (!string.IsNullOrWhiteSpace(config.ApiBaseUrl))
            {
                apiBaseUrl = config.ApiBaseUrl!;
            }
            else
            {
                throw new InvalidOperationException("'LocalApiBaseUrl/CloudApiBaseUrl' or 'ApiBaseUrl' was not found in config.json. Configure the API URL correctly.");
            }
            apiBaseUrl = apiBaseUrl.TrimEnd('/');
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error reading API configuration: {ex.Message}");
        }

        // Add HTTP client services
        builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<ClientPetApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<InventoryMovementApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<ExamApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<ExamPerformedApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<AuditApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        // AccountApiService: login/refresh without JWT handler
        builder.Services.AddHttpClient<AccountApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));

        // Services that use JWT
        builder.Services.AddHttpClient<UserManagementApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();
        builder.Services.AddHttpClient<ItemsApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<ExternalLabsApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddHttpClient<MedicalVisitApiService>(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
        builder.Services.AddStaffAuthorization();

        // Background + Theme services
        builder.Services.AddScoped<BackgroundService>();
        builder.Services.AddScoped<ThemeService>();

        // SignalR Realtime service
        builder.Services.AddScoped(sp =>
        {
            var authProvider = sp.GetRequiredService<AuthenticationStateProvider>();
            return new RealtimeService(
                apiBaseUrl,
                async () =>
                {
                    if (authProvider is JwtAuthenticationStateProvider jwtProvider)
                        return await jwtProvider.GetTokenAsync();
                    return null;
                }
            );
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    public class ConfigModel
    {
        public string? ApiBaseUrl { get; set; }
        public string? LocalApiBaseUrl { get; set; }
        public string? CloudApiBaseUrl { get; set; }
    }
}
