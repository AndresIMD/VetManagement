using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Outside Development an unhandled exception must become a ProblemDetails 500 that never leaks internals.
/// </summary>
public class ErrorHandlingTests : IClassFixture<ErrorHandlingTests.ThrowingAppFactory>
{
    internal const string SecretDetail = "connection string with password=hunter2";
    private readonly ThrowingAppFactory _factory;

    public ErrorHandlingTests(ThrowingAppFactory factory) => _factory = factory;

    [Fact]
    public async Task UnhandledException_Returns500ProblemDetails_WithoutInternals()
    {
        var response = await _factory.CreateClient().GetAsync("/test/throw");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.Should().NotContain(SecretDetail).And.NotContain("InvalidOperationException");
    }

    public class ThrowingAppFactory : CustomWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly));
        }
    }
}

// Test-only endpoint; must be a top-level public class for MVC controller discovery.
[ApiController]
[AllowAnonymous]
[Route("test/throw")]
public class ThrowingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException(ErrorHandlingTests.SecretDetail);
}
