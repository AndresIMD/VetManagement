using Microsoft.Extensions.Logging;
using System.Text.Json;
using VetManagement.Staff.UI.Services.App;

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

        // Get the API base URL from config.json, prefer Local if healthy
        string apiBaseUrl;
        string? culture;
        string? logoUrl;
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (!File.Exists(configPath))
                throw new InvalidOperationException($"Configuration file was not found: {configPath}");
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ConfigModel>(configJson) ?? new ConfigModel();
            culture = config.Culture;
            logoUrl = config.LogoUrl;

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

        builder.Services.AddStaffServices(apiBaseUrl, logoUrl);
        StaffLocalization.ApplyCulture(culture);

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
        /// <summary>The clinic's language and formats, e.g. "es-CL". Absent = English.</summary>
        public string? Culture { get; set; }
        /// <summary>The clinic's logo for the app's header. Absent = the product name as text.</summary>
        public string? LogoUrl { get; set; }
    }
}
