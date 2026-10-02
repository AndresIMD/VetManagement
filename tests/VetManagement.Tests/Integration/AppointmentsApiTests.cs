using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Scheduling;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Integration;

/// <summary>Staff agenda flow against the API, using the defaults with consultation and specialist enabled.</summary>
public class AppointmentsApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin;
    private readonly HttpClient _readOnly;
    private static readonly SemaphoreSlim SettingsLock = new(1, 1);

    public AppointmentsApiTests(CustomWebAppFactory factory)
    {
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _readOnly = factory.CreateClientWithPermissions(Permissions.SCHEDULING.READ);
    }

    /// <summary>A Wednesday (the specialist's day) far enough ahead to be bookable; each test uses its own week.</summary>
    private static DateOnly Wednesday(int weeksAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14 + 7 * weeksAhead);
        while (date.DayOfWeek != DayOfWeek.Wednesday)
            date = date.AddDays(1);
        return date;
    }

    private async Task UpdateSettingsAsync(Action<JsonNode> change, Action<JsonObject>? inspectResponse = null)
    {
        await SettingsLock.WaitAsync();
        try
        {
            var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/scheduling/settings"))!;
            change(doc["settings"]!);
            var response = await _admin.PutAsJsonAsync("/api/scheduling/settings", doc);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            inspectResponse?.Invoke((await response.Content.ReadFromJsonAsync<JsonObject>())!);
        }
        finally
        {
            SettingsLock.Release();
        }
    }

    private Task EnableAgendaAsync() => UpdateSettingsAsync(s =>
    {
        s["enabled"] = true;
        foreach (var service in s["services"]!.AsArray())
            service!["enabled"] = service["code"]!.GetValue<string>() is "consultation" or "specialist";
    });

    private async Task<List<AvailableSlotDto>> SlotsAsync(string service, DateOnly date)
        => (await _admin.GetFromJsonAsync<List<AvailableSlotDto>>($"/api/scheduling/availability?service={service}&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"))!;

    private Task<HttpResponseMessage> BookAsync(HttpClient client, string service, AvailableSlotDto slot, bool overbook = false, DateTime? startUtc = null)
        => client.PostAsJsonAsync("/api/scheduling/appointments", new
        {
            ServiceCode = service, slot.ResourceCode, StartUtc = startUtc ?? slot.StartUtc, Overbook = overbook, OwnerName = "Ana Lopez", PetName = "Luna"
        });

    private static async Task<int> IdOf(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();

    [Fact]
    public async Task Booking_TakesTheSlot_SecondBookingConflicts_OverbookIsAllowed()
    {
        await EnableAgendaAsync();
        var slot = (await SlotsAsync("specialist", Wednesday(0))).First();

        (await BookAsync(_admin, "specialist", slot)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SlotsAsync("specialist", Wednesday(0))).Should().NotContain(s => s.StartUtc == slot.StartUtc);
        (await BookAsync(_admin, "specialist", slot)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var overbooked = await BookAsync(_admin, "specialist", slot, overbook: true);
        overbooked.StatusCode.Should().Be(HttpStatusCode.OK);
        var overbookedId = await IdOf(overbooked);
        var list = await _admin.GetFromJsonAsync<List<AppointmentDto>>(
            $"/api/scheduling/appointments?fromUtc={slot.StartUtc:O}&toUtc={slot.EndUtc:O}&resource={slot.ResourceCode}");
        list.Should().Contain(a => a.Id == overbookedId && a.IsOverbooked);
    }

    [Fact]
    public async Task Booking_OutsideTheOfferedSlots_OrForADisabledService_IsRejected()
    {
        await EnableAgendaAsync();
        var slot = (await SlotsAsync("specialist", Wednesday(1))).First();

        (await BookAsync(_admin, "specialist", slot, startUtc: slot.StartUtc.AddMinutes(7))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BookAsync(_admin, "grooming", slot)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_FreesTheSlot_AndReschedule_MovesIt()
    {
        await EnableAgendaAsync();
        var slots = await SlotsAsync("specialist", Wednesday(2));
        var first = slots[0];
        var second = slots.First(s => s.StartUtc >= first.EndUtc); // must not overlap the first booking

        var id = await IdOf(await BookAsync(_admin, "specialist", first));
        (await _admin.PostAsJsonAsync($"/api/scheduling/appointments/{id}/reschedule", new { first.ResourceCode, second.StartUtc }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var afterMove = await SlotsAsync("specialist", Wednesday(2));
        afterMove.Should().Contain(s => s.StartUtc == first.StartUtc).And.NotContain(s => s.StartUtc == second.StartUtc);

        (await _admin.PostAsJsonAsync($"/api/scheduling/appointments/{id}/cancel", new { Reason = "Client called" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SlotsAsync("specialist", Wednesday(2))).Should().Contain(s => s.StartUtc == second.StartUtc);
    }

    [Fact]
    public async Task AddingAnAbsence_FlagsThatDaysAppointments_ForReschedule()
    {
        await EnableAgendaAsync();
        var date = Wednesday(3);
        var slot = (await SlotsAsync("specialist", date)).First();
        var id = await IdOf(await BookAsync(_admin, "specialist", slot));

        var flagged = -1;
        await UpdateSettingsAsync(
            s => s["exceptions"]!.AsArray().Add(new JsonObject
            {
                ["from"] = date.ToString("yyyy-MM-dd"), ["to"] = date.ToString("yyyy-MM-dd"),
                ["resourceCode"] = slot.ResourceCode, ["kind"] = "Absence", ["reason"] = "Medical leave"
            }),
            response => flagged = response["appointmentsNeedingReschedule"]!.GetValue<int>());

        flagged.Should().BeGreaterThanOrEqualTo(1);
        var needing = await _admin.GetFromJsonAsync<List<AppointmentDto>>(
            $"/api/scheduling/appointments?fromUtc={slot.StartUtc.AddDays(-1):O}&toUtc={slot.StartUtc.AddDays(1):O}&status={AppointmentStatus.NeedsReschedule}");
        needing.Should().Contain(a => a.Id == id);
        (await SlotsAsync("specialist", date)).Should().BeEmpty("the specialist is absent that day");
    }

    [Fact]
    public async Task ReadOnlyStaff_CanSeeTheAgenda_ButNotBook()
    {
        await EnableAgendaAsync();
        var slot = (await SlotsAsync("specialist", Wednesday(4))).First();

        (await _readOnly.GetAsync($"/api/scheduling/availability?service=specialist&from={Wednesday(4):yyyy-MM-dd}&to={Wednesday(4):yyyy-MM-dd}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await BookAsync(_readOnly, "specialist", slot)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
