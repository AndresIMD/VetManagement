using VetManagement.Application.Payments;
using VetManagement.Infrastructure.Payments;

namespace VetManagement.Api.Payments;

/// <summary>
/// Chooses the clinic's payment provider from deployment configuration:
/// <code>
/// "Payments": {
///   "Provider": "WebPayPlus",               // or "Simulated" (development/demo only)
///   "WebPayPlus": { "Environment": "Production", "CommerceCode": "...", "ApiKeySecret": "(secret)" }
/// }
/// </code>
/// Misconfiguration fails at startup, never when a client is paying.
/// </summary>
public static class PaymentsSetup
{
    public static IServiceCollection AddPaymentGateway(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var provider = configuration["Payments:Provider"] ?? (environment.IsProduction() ? null : "Simulated");

        switch (provider)
        {
            case "WebPayPlus":
                var options = configuration.GetSection("Payments:WebPayPlus").Get<WebPayPlusOptions>() ?? new WebPayPlusOptions();
                if (!options.IsProduction && string.IsNullOrWhiteSpace(options.CommerceCode))
                {
                    // Transbank's public sandbox credentials until the clinic gets its own.
                    options.CommerceCode = WebPayPlusOptions.IntegrationCommerceCode;
                    options.ApiKeySecret = WebPayPlusOptions.IntegrationApiKeySecret;
                }
                if (options.IsProduction && (string.IsNullOrWhiteSpace(options.CommerceCode) || string.IsNullOrWhiteSpace(options.ApiKeySecret)))
                    throw new InvalidOperationException("Payments:WebPayPlus needs CommerceCode and ApiKeySecret for the Production environment.");
                if (environment.IsProduction() && !options.IsProduction)
                    throw new InvalidOperationException("A Production deployment must use Payments:WebPayPlus:Environment = Production (the integration sandbox charges nothing).");

                services.AddSingleton(options);
                services.AddHttpClient<IPaymentGateway, WebPayPlusGateway>(client => client.Timeout = TimeSpan.FromSeconds(30));
                break;

            case "Simulated":
                if (environment.IsProduction())
                    throw new InvalidOperationException("Payments:Provider 'Simulated' is not allowed in Production: clients could book without paying.");
                services.AddSingleton<SimulatedPaymentStore>();
                services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Payments:Provider '{provider ?? "(missing)"}' is not supported. Configure 'WebPayPlus' (or 'Simulated' outside Production).");
        }

        return services;
    }
}
