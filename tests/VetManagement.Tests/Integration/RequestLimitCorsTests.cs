using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Every call of the staff app carries a token, so the browser first sends a CORS preflight (OPTIONS) for it. Those
/// preflights counted against the per-PC request limit, about half of it for nothing, and a refused request came back
/// without the CORS headers: the browser reported a CORS error and the app a network failure. Seen in a rehearsal on a
/// clinic-like PC (2026-11): with a dozen tabs opening on one PC, some got neither data nor live updates.
/// </summary>
// Own factory instance: the rate limiter is in-memory per app, so this test can't exhaust other tests' quota.
public class RequestLimitCorsTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private const string Origin = "https://localhost"; // the test host's allowed origin

    [Fact]
    public async Task Preflights_DontCountAgainstTheLimit_AndARefusal_ReachesTheApp()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:UserPerMinute", "3"));
        var client = app.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/items/paged");
            preflight.Headers.Add("Origin", Origin);
            preflight.Headers.Add("Access-Control-Request-Method", "GET");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization");
            var answer = await client.SendAsync(preflight);
            answer.StatusCode.Should().Be(HttpStatusCode.NoContent, $"preflight {i + 1} is answered, whatever the limit");
            answer.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(Origin);
            answer.Headers.GetValues("Access-Control-Max-Age").Should().Equal(["600"], "the browser asks once in 10 minutes per call");
        }

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            using var call = new HttpRequestMessage(HttpMethod.Get, "/api/items/paged");
            call.Headers.Add("Origin", Origin);
            last = await client.SendAsync(call);
            statuses.Add(last.StatusCode);
        }

        statuses.Should().Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests],
            "the whole limit (3) was left for real calls");
        last!.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal([Origin], "so the browser lets the app read the 429");
    }
}
