using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace VetManagement.Tests.Integration;

/// <summary>Brute-force protection on POST /api/account/login.</summary>
public class LoginLockoutTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public LoginLockoutTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task AccountLocks_AfterFiveFailedAttempts_EvenForTheRightPassword()
    {
        var userName = $"lockout-{Guid.NewGuid():N}";
        const string password = "Valid#Pass12345";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            (await users.CreateAsync(new IdentityUser(userName), password)).Succeeded.Should().BeTrue();
        }
        var client = _factory.CreateClient();
        Task<HttpResponseMessage> Login(string pwd) =>
            client.PostAsJsonAsync("/api/account/login", new { Username = userName, Password = pwd });

        (await Login(password)).StatusCode.Should().Be(HttpStatusCode.OK, "the setup must allow a normal login");

        for (var i = 0; i < 5; i++)
            (await Login("wrong-password")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await Login(password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the account is locked out");
    }
}

// Own factory instance: the rate limiter is in-memory per app, so this test can't exhaust other tests' quota.
public class LoginRateLimitTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public LoginRateLimitTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task EleventhLoginWithinAMinute_IsRejectedWith429()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            var response = await client.PostAsJsonAsync("/api/account/login", new { Username = "nobody", Password = "x" });
            statuses.Add(response.StatusCode);
        }

        statuses.Take(10).Should().AllBeEquivalentTo(HttpStatusCode.Unauthorized);
        statuses.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }
}
