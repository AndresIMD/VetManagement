using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.ClientPortal;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>Clients see their pets' file through an emailed link: no account, no RUT probing, links expire.</summary>
public class ClientPortalTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _anonymous;
    private static readonly SemaphoreSlim Sequential = new(1, 1);

    public ClientPortalTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _anonymous = factory.CreateClient();
    }

    private static string NewRut()
    {
        var number = Random.Shared.Next(5_000_000, 30_000_000);
        Rut.TryParse($"{number}-{Rut.ComputeCheckDigit(number)}", out var rut).Should().BeTrue();
        return rut.ToString();
    }

    private async Task<(string Rut, string Email)> AddClientWithPetAsync()
    {
        var rut = NewRut();
        var email = $"portal-{Guid.NewGuid():N}@example.test";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new Client("Marta", "Rojas", rut, "Calle 2", 987654321, email);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        var pet = new Pet { OwnerId = client.Id, Name = "Nala", Breed = "Siamés", Species = Species.Cat };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        db.Appointments.Add(new Appointment
        {
            ServiceCode = "consultation", ResourceCode = "vet-general", ClientId = client.Id, PetId = pet.Id, PetName = "Nala", OwnerName = "Marta",
            StartUtc = DateTime.UtcNow.AddDays(3), EndUtc = DateTime.UtcNow.AddDays(3).AddMinutes(30), OccupiedUntilUtc = DateTime.UtcNow.AddDays(3).AddMinutes(30),
            Status = AppointmentStatus.Confirmed, PublicToken = Guid.NewGuid().ToString("N"), CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        (await _admin.PostAsJsonAsync($"/api/clinical/pets/{pet.Id}/doses",
            new { ProtocolCode = "cat-triple", AppliedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)) })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PostAsJsonAsync("/api/medical-visits",
            new { PatientId = pet.Id, PatientName = "Nala", Date = DateTime.UtcNow.AddDays(-10), Reason = "Vacuna", Diagnosis = "Sana", Treatment = "Ninguno" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return (rut, email);
    }

    private async Task SetPortalAsync(bool enabled, bool showDetails = true)
    {
        var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/clinical/settings"))!;
        doc["settings"]!["clientPortal"]!["enabled"] = enabled;
        doc["settings"]!["clientPortal"]!["showVisitDetails"] = showDetails;
        (await _admin.PutAsJsonAsync("/api/clinical/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private string TokenFromEmail(string email)
    {
        var body = _factory.Emails.Sent.Last(e => e.To == email).Body;
        return Regex.Match(body, @"/mis-mascotas/([A-Za-z0-9_-]+)").Groups[1].Value;
    }

    [Fact]
    public async Task LinkByEmail_OpensThePetsFile_WithVaccinesVisitsAndUpcomingAppointments()
    {
        await Sequential.WaitAsync();
        try
        {
            var (rut, email) = await AddClientWithPetAsync();
            await SetPortalAsync(enabled: true);

            (await _anonymous.PostAsJsonAsync("/api/public/portal/link", new { Rut = rut })).StatusCode.Should().Be(HttpStatusCode.Accepted);
            var token = TokenFromEmail(email);
            token.Should().HaveLength(43);

            var portal = (await _anonymous.GetFromJsonAsync<ClientPortalDto>($"/api/public/portal/{token}"))!;
            portal.ClientName.Should().Be("Marta Rojas");
            var pet = portal.Pets.Single();
            pet.Name.Should().Be("Nala");
            pet.Doses.Single().ProductName.Should().Be("Triple felina");
            pet.Visits.Single().Diagnosis.Should().Be("Sana");
            portal.Upcoming.Single().ServiceName.Should().Be("Consulta general");

            // Asking again right away doesn't flood the inbox.
            (await _anonymous.PostAsJsonAsync("/api/public/portal/link", new { Rut = rut })).StatusCode.Should().Be(HttpStatusCode.Accepted);
            _factory.Emails.Sent.Count(e => e.To == email).Should().Be(1);

            // The clinic can hide diagnosis and treatment.
            await SetPortalAsync(enabled: true, showDetails: false);
            (await _anonymous.GetFromJsonAsync<ClientPortalDto>($"/api/public/portal/{token}"))!.Pets.Single().Visits.Single().Diagnosis.Should().BeNull();
        }
        finally
        {
            await SetPortalAsync(enabled: false);
            Sequential.Release();
        }
    }

    [Fact]
    public async Task UnknownRut_GetsTheSameAnswer_InvalidRutIsRejected_AndBadOrExpiredLinksFail()
    {
        await Sequential.WaitAsync();
        try
        {
            var (rut, email) = await AddClientWithPetAsync();
            await SetPortalAsync(enabled: true);
            var before = _factory.Emails.Sent.Count;

            (await _anonymous.PostAsJsonAsync("/api/public/portal/link", new { Rut = NewRut() })).StatusCode.Should().Be(HttpStatusCode.Accepted);
            _factory.Emails.Sent.Count.Should().Be(before, "nobody to email, but the answer doesn't say so");
            (await _anonymous.PostAsJsonAsync("/api/public/portal/link", new { Rut = "12.345.678-0" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await _anonymous.GetAsync("/api/public/portal/not-a-real-token")).StatusCode.Should().Be(HttpStatusCode.NotFound);

            (await _anonymous.PostAsJsonAsync("/api/public/portal/link", new { Rut = rut })).StatusCode.Should().Be(HttpStatusCode.Accepted);
            var token = TokenFromEmail(email);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.ClientAccessTokens.ForEachAsync(t => t.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1));
                await db.SaveChangesAsync();
            }
            (await _anonymous.GetAsync($"/api/public/portal/{token}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await SetPortalAsync(enabled: false);
            Sequential.Release();
        }
    }

    [Fact]
    public async Task Portal_IsOffUntilTheClinicEnablesIt()
        => (await _anonymous.PostAsJsonAsync("/api/public/portal/link", new { Rut = NewRut() })).StatusCode.Should().Be(HttpStatusCode.NotFound);
}
