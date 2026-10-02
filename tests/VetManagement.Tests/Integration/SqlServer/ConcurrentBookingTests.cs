using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

public class ConcurrentBookingTests
{
    [SqlServerFact]
    public async Task ParallelBookings_ForTheLastSlot_OnlyOneSucceeds()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
            var doc = (await admin.GetFromJsonAsync<JsonObject>("/api/scheduling/settings"))!;
            doc["settings"]!["enabled"] = true;
            foreach (var service in doc["settings"]!["services"]!.AsArray())
                service!["enabled"] = service["code"]!.GetValue<string>() == "specialist";
            (await admin.PutAsJsonAsync("/api/scheduling/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);

            var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
            while (date.DayOfWeek != DayOfWeek.Wednesday)
                date = date.AddDays(1);
            var slot = (await admin.GetFromJsonAsync<List<AvailableSlotDto>>(
                $"/api/scheduling/availability?service=specialist&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"))!.First();

            var attempts = Enumerable.Range(0, 5).Select(i => factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")])
                .PostAsJsonAsync("/api/scheduling/appointments", new
                {
                    ServiceCode = "specialist", slot.ResourceCode, slot.StartUtc, OwnerName = $"Client {i}"
                }));
            var statuses = (await Task.WhenAll(attempts)).Select(r => r.StatusCode).ToList();

            statuses.Count(s => s == HttpStatusCode.OK).Should().Be(1, $"exactly one booking may win, got [{string.Join(", ", statuses)}]");
            statuses.Should().OnlyContain(s => s == HttpStatusCode.OK || s == HttpStatusCode.Conflict);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
