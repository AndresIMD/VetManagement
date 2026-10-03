using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.TestData;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>Sample data is created in dependency order; each kind explains what it needs first.</summary>
public class TestDataTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

    private Task<HttpResponseMessage> CreateAsync(string key, int count = 100) => _admin.PostAsync($"/api/dev/test-data/{key}?count={count}", null);

    private static async Task<string> ProblemTitleAsync(HttpResponseMessage response) => (await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task ExamRequests_NeedExamsAndPets_PetsNeedClients_ThenEachRequestGoesToADifferentPet()
    {
        var first = await CreateAsync("exam-requests");
        first.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemTitleAsync(first)).Should().Contain("exam catalog").And.Contain("pets");

        (await CreateAsync("exams")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ProblemTitleAsync(await CreateAsync("exam-requests"))).Should().NotContain("exam catalog").And.Contain("pets");

        var pets = await CreateAsync("pets");
        pets.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemTitleAsync(pets)).Should().Contain("clients");

        (await CreateAsync("clients")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CreateAsync("pets")).StatusCode.Should().Be(HttpStatusCode.OK);

        var requests = await CreateAsync("exam-requests");
        requests.StatusCode.Should().Be(HttpStatusCode.OK);
        (await requests.Content.ReadFromJsonAsync<TestDataCreatedDto>())!.Created.Should().Be(100);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ExamsPerformed.Select(r => r.PatientId).Distinct().CountAsync()).Should().Be(100, "100 pets, one request each");
        (await db.Clients.Select(c => c.TaxId).Distinct().CountAsync()).Should().Be(100);
        (await db.Pets.Select(p => p.OwnerId).Where(o => !db.Clients.Any(c => c.Id == o)).CountAsync()).Should().Be(0);

        var status = (await _admin.GetFromJsonAsync<List<TestDataStatusDto>>("/api/dev/test-data"))!;
        status.Single(s => s.Key == "exam-requests").Should().Match<TestDataStatusDto>(s => s.Existing == 100 && s.Missing.Count == 0);
    }

    [Fact]
    public async Task EveryGenerator_CreatesItsRecords_InOrder()
    {
        foreach (var key in new[] { "clients", "pets", "exams", "external-labs", "exam-requests", "inventory", "visits", "doses", "appointments", "sales" })
        {
            var response = await CreateAsync(key, 20);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{key}: {await response.Content.ReadAsStringAsync()}");
            (await response.Content.ReadFromJsonAsync<TestDataCreatedDto>())!.Created.Should().Be(20, key);
        }

        (await _admin.GetAsync("/api/reports/summary?from=2026-01-01&to=2026-12-31")).StatusCode.Should().Be(HttpStatusCode.OK, "the data is usable by the app");
        (await CreateAsync("clients", 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreateAsync("dragons")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.CreateClientWithPermissions(Permissions.ForRole("Manager").ToArray()).GetAsync("/api/dev/test-data"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
