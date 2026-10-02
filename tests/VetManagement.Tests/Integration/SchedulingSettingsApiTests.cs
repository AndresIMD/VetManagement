using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using VetManagement.Api.Authorization;

namespace VetManagement.Tests.Integration;

public class SchedulingSettingsApiTests : IClassFixture<CustomWebAppFactory>
{
    private const string Url = "/api/scheduling/settings";
    private readonly HttpClient _admin;
    private readonly HttpClient _employee;

    public SchedulingSettingsApiTests(CustomWebAppFactory factory)
    {
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _employee = factory.CreateClientWithPermissions([.. Permissions.ForRole("Employee")]);
    }

    private async Task<JsonObject> GetDocumentAsync(HttpClient client)
    {
        var response = await client.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task FirstRead_ReturnsTheDefaults_InTheReadableFormat()
    {
        var doc = await GetDocumentAsync(_employee);

        doc["version"]!.GetValue<int>().Should().BeGreaterThan(0);
        doc["settings"]!["timeZone"]!.GetValue<string>().Should().Be("America/Santiago");
        doc["settings"]!["resources"]![0]!["kind"]!.GetValue<string>().Should().Be("Vet", "enums are names, as in the defaults file");
    }

    [Fact]
    public async Task Save_BumpsTheVersion_AndAStaleSaveIsAConflict()
    {
        var doc = await GetDocumentAsync(_admin);
        var version = doc["version"]!.GetValue<int>();
        doc["settings"]!["enabled"] = true;

        var saved = await _admin.PutAsJsonAsync(Url, doc);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var reread = await GetDocumentAsync(_admin);
        reread["version"]!.GetValue<int>().Should().Be(version + 1);
        reread["settings"]!["enabled"]!.GetValue<bool>().Should().BeTrue();

        // Same document again, still claiming the old version: someone else's save must not be overwritten.
        (await _admin.PutAsJsonAsync(Url, doc)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvalidSettings_AreRejected_WithTheReason()
    {
        var doc = await GetDocumentAsync(_admin);
        doc["settings"]!["services"]![0]!["depositPercent"] = 150;

        var response = await _admin.PutAsJsonAsync(Url, doc);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DepositPercent");
    }

    [Fact]
    public async Task OnlyAdmins_CanSave()
    {
        var doc = await GetDocumentAsync(_employee);

        (await _employee.PutAsJsonAsync(Url, doc)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
