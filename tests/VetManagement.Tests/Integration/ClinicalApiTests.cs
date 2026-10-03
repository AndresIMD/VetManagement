using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Clinical;
using VetManagement.Contracts.Clinical;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Clinical;
using VetManagement.Domain.Enums;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>The pet's clinical file: visits, vaccines/deworming with next due dates, due list and owner reminders.</summary>
public class ClinicalApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _employee;

    public ClinicalApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _employee = factory.CreateClientWithPermissions([.. Permissions.ForRole("Employee")]);
    }

    // Clinic time zone (America/Santiago) is behind UTC, so its "today" is never after UTC's.
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    private async Task<(int PetId, string Email)> AddPetAsync(Species species = Species.Dog)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = $"owner-{Guid.NewGuid():N}@example.test";
        var owner = new Client("Ana", "López", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, email);
        db.Clients.Add(owner);
        await db.SaveChangesAsync();
        var pet = new Pet { OwnerId = owner.Id, Name = "Luna", Breed = "Mestizo", Species = species };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        return (pet.Id, email);
    }

    private static async Task<int> IdOf(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    private async Task<PetHistoryDto> HistoryAsync(int petId) => (await _admin.GetFromJsonAsync<PetHistoryDto>($"/api/clinical/pets/{petId}/history"))!;

    [Fact]
    public async Task DoseFromProtocol_GetsNextDueDate_AndALaterDoseReplacesIt()
    {
        var (petId, _) = await AddPetAsync();
        var first = Today.AddDays(-400);

        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "rabies", AppliedOn = first, BatchNumber = "L-1" }));
        var history = await HistoryAsync(petId);
        var dose = history.Doses.Single();
        dose.ProductName.Should().Be("Antirrábica");
        dose.Kind.Should().Be(PreventiveKind.Vaccine);
        dose.NextDueOn.Should().Be(first.AddDays(365));
        dose.Status.Should().Be(PreventiveStatus.Overdue);

        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "rabies", AppliedOn = Today }));
        history = await HistoryAsync(petId);
        history.Doses.Should().HaveCount(2);
        history.Doses[0].Status.Should().Be(PreventiveStatus.UpToDate, "newest first");
        history.Doses[1].Status.Should().Be(PreventiveStatus.Superseded);
    }

    [Theory]
    [InlineData(null, null, 0, "protocol or enter the product")]
    [InlineData("unknown", null, 0, "Unknown protocol")]
    [InlineData(null, "Bravecto", 2, "future")]
    public async Task InvalidDoses_AreRejected(string? protocol, string? product, int daysAhead, string error)
    {
        var (petId, _) = await AddPetAsync();
        var response = await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses",
            new { ProtocolCode = protocol, ProductName = product, AppliedOn = Today.AddDays(daysAhead) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(error);
    }

    [Fact]
    public async Task OneOffProduct_WithOwnNextDate_IsKept()
    {
        var (petId, _) = await AddPetAsync();
        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses",
            new { ProductName = "Bravecto", Kind = PreventiveKind.Deworming, AppliedOn = Today, NextDueOn = Today.AddDays(84) }));

        var dose = (await HistoryAsync(petId)).Doses.Single();
        dose.ProtocolCode.Should().BeNull();
        dose.Kind.Should().Be(PreventiveKind.Deworming);
        dose.NextDueOn.Should().Be(Today.AddDays(84));
    }

    [Fact]
    public async Task DueDoses_AreListed_AndTheOwnerIsRemindedOnce()
    {
        var (petId, email) = await AddPetAsync();
        // Due in 5 days: inside the default 7-day reminder window.
        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "rabies", AppliedOn = Today.AddDays(-360) }));
        // Replaced by a later dose: never due, never reminded.
        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "deworm-internal", AppliedOn = Today.AddDays(-100) }));
        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "deworm-internal", AppliedOn = Today }));

        var due = (await _employee.GetFromJsonAsync<List<DueDoseDto>>("/api/clinical/due?days=30"))!.Where(d => d.Dose.PetId == petId).ToList();
        due.Should().ContainSingle();
        due[0].Dose.Status.Should().Be(PreventiveStatus.DueSoon);
        due[0].OwnerEmail.Should().Be(email);

        await SendRemindersAsync();
        await SendRemindersAsync();

        var sent = _factory.Emails.Sent.Where(e => e.To == email).ToList();
        sent.Should().ContainSingle();
        sent[0].Body.Should().Contain("Antirrábica").And.Contain("Luna");
        (await HistoryAsync(petId)).Doses.Should().Contain(d => d.ProtocolCode == "rabies" && d.ReminderSentAtUtc != null);
    }

    private async Task SendRemindersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClinicalService>().SendDueRemindersAsync();
    }

    [Fact]
    public async Task VisitWithClinicalNotes_IsInTheHistory_AndUpdatesThePetWeight()
    {
        var (petId, _) = await AddPetAsync();
        (await _employee.PostAsJsonAsync("/api/medical-visits", new
        {
            PatientId = petId, PatientName = "Luna", Date = DateTime.UtcNow,
            Reason = "Vómitos", Anamnesis = "Desde ayer", Examination = "Abdomen sensible", Diagnosis = "Gastritis",
            Treatment = "Dieta blanda 3 días", WeightKg = 12.4m, TemperatureC = 39.1m
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await HistoryAsync(petId);
        var visit = history.Visits.Single();
        visit.Diagnosis.Should().Be("Gastritis");
        visit.TemperatureC.Should().Be(39.1m);
        history.Weight.Should().BeApproximately(12.4f, 0.01f);
    }

    [Fact]
    public async Task Settings_OnlyAdminsChangeThem_AndTheyAreValidated()
    {
        var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/clinical/settings"))!;
        doc["settings"]!["protocols"]!.AsArray().Should().NotBeEmpty();

        (await _employee.PutAsJsonAsync("/api/clinical/settings", doc)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var invalid = doc.DeepClone();
        invalid["settings"]!["reminders"]!["daysBefore"] = 500;
        (await _admin.PutAsJsonAsync("/api/clinical/settings", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PutAsJsonAsync("/api/clinical/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(-1, PreventiveStatus.Overdue)]
    [InlineData(0, PreventiveStatus.DueSoon)]
    [InlineData(7, PreventiveStatus.DueSoon)]
    [InlineData(8, PreventiveStatus.UpToDate)]
    public void DoseStatus_FollowsTheReminderWindow(int dueInDays, PreventiveStatus expected)
    {
        var today = new DateOnly(2026, 10, 3);
        new PreventiveDose { NextDueOn = today.AddDays(dueInDays) }.StatusOn(today, 7, superseded: false).Should().Be(expected);
    }
}
