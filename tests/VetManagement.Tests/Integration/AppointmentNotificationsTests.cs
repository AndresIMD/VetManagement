using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Scheduling;
using VetManagement.Contracts.Scheduling;

namespace VetManagement.Tests.Integration;

/// <summary>Client emails for appointments, each switched on/off by the clinic's notification settings.</summary>
public class AppointmentNotificationsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private static readonly SemaphoreSlim Serial = new(1, 1);

    public AppointmentNotificationsTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
    }

    private static DateOnly Wednesday(int weeksAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14 + 7 * weeksAhead);
        while (date.DayOfWeek != DayOfWeek.Wednesday)
            date = date.AddDays(1);
        return date;
    }

    private async Task ConfigureAsync(bool confirmation = true, bool reminder = true, bool changes = true, int reminderHours = 24)
    {
        var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/scheduling/settings"))!;
        var s = doc["settings"]!;
        s["enabled"] = true;
        s["clinicName"] = "San Pablo Vet Clinic";
        s["clinicPhone"] = "+56 2 2345 6789";
        foreach (var service in s["services"]!.AsArray())
            service!["enabled"] = service["code"]!.GetValue<string>() == "specialist";
        s["notifications"] = new JsonObject
        {
            ["sendConfirmation"] = confirmation, ["sendReminder"] = reminder,
            ["reminderHoursBefore"] = reminderHours, ["sendRescheduleNotice"] = changes
        };
        (await _admin.PutAsJsonAsync("/api/scheduling/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(int Id, AvailableSlotDto Slot, List<AvailableSlotDto> All)> BookAsync(int weeksAhead, string? email = "ana@mail.cl")
    {
        var date = Wednesday(weeksAhead);
        var slots = (await _admin.GetFromJsonAsync<List<AvailableSlotDto>>(
            $"/api/scheduling/availability?service=specialist&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}"))!;
        var response = await _admin.PostAsJsonAsync("/api/scheduling/appointments", new
        {
            ServiceCode = "specialist", slots[0].ResourceCode, slots[0].StartUtc, OwnerName = "Ana Lopez", OwnerEmail = email, PetName = "Luna"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        return (id, slots[0], slots);
    }

    private List<SentEmail> EmailsTo(string to) => _factory.Emails.Sent.Where(e => e.To == to).ToList();

    [Fact]
    public async Task Booking_SendsASpanishConfirmation_WithTheLocalTime()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var (_, slot, _) = await BookAsync(0, "confirm@mail.cl");

            var email = EmailsTo("confirm@mail.cl").Should().ContainSingle().Subject;
            var local = TimeZoneInfo.ConvertTimeFromUtc(slot.StartUtc, TimeZoneInfo.FindSystemTimeZoneById("America/Santiago"));
            email.Subject.Should().Be("Confirmación de tu hora – San Pablo Vet Clinic");
            email.Body.Should().Contain("Hola Ana Lopez").And.Contain("Consulta especialista").And.Contain($"Hora: {local:HH:mm}")
                .And.Contain("miércoles").And.Contain("Paciente: Luna").And.Contain("+56 2 2345 6789");
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task NoConfirmation_WhenSwitchedOff_OrWithoutAnEmail()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync(confirmation: false);
            await BookAsync(1, "off@mail.cl");
            EmailsTo("off@mail.cl").Should().BeEmpty();

            await ConfigureAsync(confirmation: true);
            var before = _factory.Emails.Sent.Count;
            await BookAsync(2, email: null);
            _factory.Emails.Sent.Count.Should().Be(before, "there is no address to send to");
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task RescheduleAndCancel_NotifyTheClient_UnlessSwitchedOff()
    {
        await Serial.WaitAsync();
        try
        {
            await ConfigureAsync();
            var (id, first, all) = await BookAsync(3, "changes@mail.cl");
            var later = all.First(s => s.StartUtc >= first.EndUtc);

            (await _admin.PostAsJsonAsync($"/api/scheduling/appointments/{id}/reschedule", new { later.ResourceCode, later.StartUtc })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _admin.PostAsJsonAsync($"/api/scheduling/appointments/{id}/cancel", new { Reason = "Vet sick" })).StatusCode.Should().Be(HttpStatusCode.OK);
            EmailsTo("changes@mail.cl").Select(e => e.Subject).Should().Contain(s => s.StartsWith("Tu hora cambió")).And.Contain(s => s.StartsWith("Tu hora fue cancelada"));

            await ConfigureAsync(changes: false);
            var (id2, _, _) = await BookAsync(4, "quiet@mail.cl");
            (await _admin.PostAsJsonAsync($"/api/scheduling/appointments/{id2}/cancel", new { Reason = "x" })).StatusCode.Should().Be(HttpStatusCode.OK);
            EmailsTo("quiet@mail.cl").Should().OnlyContain(e => e.Subject.StartsWith("Confirmación"));
        }
        finally { Serial.Release(); }
    }

    [Fact]
    public async Task Reminders_AreSentOnce_ForAppointmentsInsideTheWindow()
    {
        await Serial.WaitAsync();
        try
        {
            // A window wide enough to include the test appointment a few weeks ahead.
            await ConfigureAsync(confirmation: false, reminderHours: 24 * 120);
            await BookAsync(5, "reminder@mail.cl");

            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AppointmentService>();
            await service.SendDueRemindersAsync();
            await service.SendDueRemindersAsync();

            EmailsTo("reminder@mail.cl").Should().ContainSingle(e => e.Subject.StartsWith("Recordatorio"), "a reminder is sent only once");
        }
        finally { Serial.Release(); }
    }
}
