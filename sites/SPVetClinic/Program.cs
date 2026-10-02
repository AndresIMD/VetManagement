using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SPVetClinic;
using SPVetClinic.Data;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Booking buttons: the clinic's booking page. "{returnUrl}" lets the VetManagement booking portal send
// clients back to this site after booking (the portal only accepts origins the clinic allows).
if (builder.Configuration["BookingUrl"] is { Length: > 0 } bookingUrl)
    AppConstants.Clinic.BookingUrl = bookingUrl.Replace("{returnUrl}", Uri.EscapeDataString(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
