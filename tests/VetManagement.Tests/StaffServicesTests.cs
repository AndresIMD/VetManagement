using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using VetManagement.Staff.UI.Services.Api;
using VetManagement.Staff.UI.Services.App;
using VetManagement.Staff.UI.Services.Auth;

namespace VetManagement.Tests;

public class StaffServicesTests
{
    // These come with the Blazor host, in the browser and in the desktop app.
    private static readonly Type[] FromTheHost = [typeof(NavigationManager), typeof(IJSRuntime)];

    // A component that injects an unregistered service compiles, then fails when it renders: in the layout that's a
    // blank app.
    [Fact]
    public void EveryServiceAComponentInjects_IsRegistered()
    {
        var registered = new ServiceCollection().AddStaffServices("http://localhost/").Select(s => s.ServiceType).ToHashSet();

        var missing = typeof(StaffServices).Assembly.GetTypes()
            .Where(typeof(IComponent).IsAssignableFrom)
            .SelectMany(component => component
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(property => property.IsDefined(typeof(InjectAttribute)))
                .Select(property => (Component: component.Name, Service: property.PropertyType)))
            .Where(injected => !registered.Contains(injected.Service)
                && !FromTheHost.Contains(injected.Service)
                && !(injected.Service.IsGenericType && registered.Contains(injected.Service.GetGenericTypeDefinition())))
            .Select(injected => $"{injected.Component} injects {injected.Service.Name}")
            .Distinct();

        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task WhereJavaScriptCantRun_ApiCallsStillGetTheSessionsToken()
    {
        var session = new SessionToken();
        var inTheWebView = new AccountApiService(new HttpClient(), new JsWithAToken("the token"), session);
        var inAnHttpHandler = new AccountApiService(new HttpClient(), new JsOutsideTheWebView(), session);

        (await inTheWebView.GetTokenAsync()).Should().Be("the token");

        (await inAnHttpHandler.GetTokenAsync()).Should().Be("the token");
    }

    [Fact]
    public async Task AfterLoggingOut_NoCallGetsTheOldToken()
    {
        var session = new SessionToken { Value = "the token" };

        await new AccountApiService(new HttpClient(), new JsWithAToken(null), session).LogoutAsync();

        (await new AccountApiService(new HttpClient(), new JsOutsideTheWebView(), session).GetTokenAsync()).Should().BeNull();
    }

    private sealed class JsWithAToken(string? token) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult((TValue)(object?)token!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class JsOutsideTheWebView : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("Cannot invoke JavaScript outside of a WebView context.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
