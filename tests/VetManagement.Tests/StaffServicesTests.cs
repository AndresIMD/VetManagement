using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using VetManagement.Staff.UI.Services.App;

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
}
