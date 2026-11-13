namespace VetManagement.Api.TestData;

/// <summary>
/// Where sample data may be created: never in Production; elsewhere only in Development or Testing, or when
/// <c>TestData:Enabled</c> is true (e.g. a demo server). The endpoints and the permission both follow this one rule, so
/// where it says no, nobody is offered a page that would answer 404.
/// </summary>
public static class TestDataAvailability
{
    public static bool IsAvailable(IHostEnvironment environment, IConfiguration configuration)
        => !environment.IsProduction()
            && (environment.IsDevelopment() || environment.IsEnvironment("Testing") || configuration.GetValue<bool>("TestData:Enabled"));
}
