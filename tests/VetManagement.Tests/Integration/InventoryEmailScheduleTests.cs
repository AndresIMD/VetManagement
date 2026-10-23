using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using VetManagement.Api.Authorization;
using VetManagement.Api.Services;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;

namespace VetManagement.Tests.Integration;

/// <summary>The inventory alert schedule is a settings document in the database, so a redeploy doesn't lose it.</summary>
public class InventoryEmailScheduleTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

    [Fact]
    public async Task Schedule_IsSavedAndRead_AndAnEnabledOneNeedsAnAddress()
    {
        var withoutAddress = await _admin.PostAsJsonAsync("/api/inventory-alerts/schedule", new InventoryEmailSchedule { Enabled = true, DayOfWeek = 1, Hour = 9 });
        withoutAddress.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await withoutAddress.Content.ReadAsStringAsync()).Should().Contain("email address");

        (await _admin.PostAsJsonAsync("/api/inventory-alerts/schedule",
            new InventoryEmailSchedule { Enabled = true, DayOfWeek = 1, Hour = 9, Minute = 30, To = "bodega@example.test" })).EnsureSuccessStatusCode();

        var saved = (await _admin.GetFromJsonAsync<InventoryEmailSchedule>("/api/inventory-alerts/schedule"))!;
        (saved.Enabled, saved.DayOfWeek, saved.Hour, saved.Minute, saved.To).Should().Be((true, 1, 9, 30, "bodega@example.test"));
    }

    [Fact]
    public async Task ScheduleSavedInTheFormerFile_StartsTheDocument()
    {
        await using var clinic = new CustomWebAppFactory(); // its own database: no schedule stored yet
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "App_Data"));
            await File.WriteAllTextAsync(Path.Combine(root, "App_Data", "inventory_email_schedule.json"),
                JsonSerializer.Serialize(new InventoryEmailSchedule { Enabled = true, DayOfWeek = 5, Hour = 18, To = "jefa@example.test" }));
            using var scope = clinic.Services.CreateScope();
            var service = new InventoryEmailScheduleService(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                scope.ServiceProvider.GetRequiredService<AuditService>(), new Environment(root));

            var schedule = (await service.GetAsync()).Settings;

            (schedule.Enabled, schedule.DayOfWeek, schedule.Hour, schedule.To).Should().Be((true, 5, 18, "jefa@example.test"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class Environment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "VetManagement.Api";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
