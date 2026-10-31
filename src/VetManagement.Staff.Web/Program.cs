using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using VetManagement.Staff.UI.Services.App;
using VetManagement.Staff.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Helpers to sanitize config values
static bool IsPlaceholder(string? value)
 => !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("<") && value.Trim().EndsWith(">");

static string? Normalize(string? value)
 => string.IsNullOrWhiteSpace(value) || IsPlaceholder(value) ? null : value.Trim();

// Load API base URL and culture from wwwroot/config.json; prefer Local if health check succeeds
string? apiBaseUrl = null;
string? culture = null;
string? logoUrl = null;
try
{
    using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

    // Avoid browser/service worker caching when fetching config.json
    var cacheBuster = $"?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    var configJson = await http.GetStringAsync($"config.json{cacheBuster}");
    var config = JsonSerializer.Deserialize<ConfigModel>(configJson) ?? new ConfigModel();
    culture = Normalize(config.Culture);
    logoUrl = Normalize(config.LogoUrl);

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

builder.Services.AddStaffServices(apiBaseUrl, logoUrl);

var host = builder.Build();
StaffLocalization.ApplyCulture(culture);
await host.RunAsync();

namespace VetManagement.Staff.Web
{
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
