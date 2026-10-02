using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using VetManagement.Booking.Web;
using VetManagement.Booking.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Clients read dates in Spanish (es-CL).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-CL");

// Each clinic deployment points the portal at its own API (wwwroot/appsettings.json or the environment's file).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<BookingApi>();
builder.Services.AddScoped<PortalState>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
