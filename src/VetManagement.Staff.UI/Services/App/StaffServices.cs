using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using VetManagement.Staff.UI.Models.Configuration;
using VetManagement.Staff.UI.Services.Api;
using VetManagement.Staff.UI.Services.Auth;
using VetManagement.Staff.UI.Services.Realtime;

namespace VetManagement.Staff.UI.Services.App;

/// <summary>
/// The services of the staff UI, one list for every host: a page that works in the browser also works in the desktop
/// app. <c>StaffServicesTests</c> checks that every service a component injects is here.
/// </summary>
public static class StaffServices
{
    /// <param name="apiBaseUrl">Empty when config.json has no valid URL: the layout then shows the error.</param>
    public static IServiceCollection AddStaffServices(this IServiceCollection services, string? apiBaseUrl)
    {
        services.AddMudServices();
        services.AddStaffLocalization();
        services.AddScoped<AppStatusService>();

        var noApi = string.IsNullOrWhiteSpace(apiBaseUrl);
        services.AddSingleton(new AppBootstrapError(
            noApi ? "Configuration error" : string.Empty,
            noApi ? "config.json is missing valid API base URLs. Set 'LocalApiBaseUrl' (e.g., https://localhost:44395) or 'ApiBaseUrl'." : string.Empty));

        services.AddSingleton<SessionToken>();
        services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
        services.AddStaffAuthorization();
        services.AddScoped<SessionRefresher>();
        services.AddTransient<JwtAuthorizationMessageHandler>();

        services.AddScoped(sp =>
        {
            var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
            return new RealtimeService(apiBaseUrl ?? string.Empty, async () =>
                authStateProvider is JwtAuthenticationStateProvider jwtProvider ? await jwtProvider.GetTokenAsync() : null);
        });

        void AddApi<TService>() where TService : class
            => services.AddHttpClient<TService>(client => SetBaseAddress(client, apiBaseUrl))
                .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

        AddApi<ClientPetApiService>();
        AddApi<InventoryMovementApiService>();
        AddApi<WarehousesApiService>();
        AddApi<ExamApiService>();
        AddApi<ExamPerformedApiService>();
        AddApi<ExternalLabsApiService>();
        AddApi<AuditApiService>();
        AddApi<SchedulingApiService>();
        AddApi<BillingApiService>();
        AddApi<ClinicalApiService>();
        AddApi<ReportsApiService>();
        AddApi<ExamReportsApiService>();
        AddApi<ConsumptionApiService>();
        AddApi<TestDataApiService>();
        AddApi<UserManagementApiService>();
        AddApi<MedicalVisitApiService>();
        AddApi<ItemsApiService>();
        AddApi<AlertsApiService>();
        AddApi<InventoryAlertsApiService>();

        // Login and refresh go without the JWT handler
        services.AddHttpClient<AccountApiService>(client => SetBaseAddress(client, apiBaseUrl));

        services.AddScoped<BackgroundService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<TourService>();

        return services;
    }

    private static void SetBaseAddress(HttpClient client, string? apiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            client.BaseAddress = new Uri(apiBaseUrl);
    }
}
