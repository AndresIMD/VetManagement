using System.Net;
using FluentAssertions;

namespace VetManagement.Tests.Integration;

public class HealthCheckTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public HealthCheckTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Healthz_IsHealthy_WhenDatabaseIsReachable()
    {
        var response = await _factory.CreateClient().GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
