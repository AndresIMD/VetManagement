using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Integration;

/// <summary>The lab machines the exam catalog picks from: a rename reaches the exams, a machine in use stays.</summary>
public class LabMachinesApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _supervisor = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.LABORATORY, Permissions.ROLES.SUPERVISOR])]);

    private static string Unique(string name) => $"{name}-{Guid.NewGuid():N}"[..20];

    private async Task<int> AddAsync(string brand, string model)
    {
        var response = await _supervisor.PostAsJsonAsync("/api/lab-machines", new LabMachineRequest { Brand = brand, Model = model });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<LabMachineDto> GetAsync(int id)
        => (await _supervisor.GetFromJsonAsync<List<LabMachineDto>>("/api/lab-machines"))!.Single(m => m.Id == id);

    [Fact]
    public async Task Rename_CarriesOverToTheExams_AndAMachineInUseCannotBeDeleted()
    {
        var (brand, model) = (Unique("Brand"), Unique("Model"));
        var id = await AddAsync(brand, model);
        (await _supervisor.PostAsJsonAsync("/api/exams", new ExamRequest
        {
            Name = Unique("Exam"), Brand = brand, Machine = model, SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube
        })).EnsureSuccessStatusCode();
        (await GetAsync(id)).ExamCount.Should().Be(1);

        var deleted = await _supervisor.DeleteAsync($"/api/lab-machines/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await deleted.Content.ReadAsStringAsync()).Should().Contain("Move them to another machine first");

        var renamed = await _supervisor.PutAsJsonAsync($"/api/lab-machines/{id}", new LabMachineRequest { Brand = brand, Model = model + " Pro" });
        renamed.EnsureSuccessStatusCode();
        (await renamed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("examsUpdated").GetInt32().Should().Be(1);
        var exams = await _supervisor.GetFromJsonAsync<List<ExamDto>>("/api/exams");
        exams!.Should().ContainSingle(e => e.Brand == brand).Which.Machine.Should().Be(model + " Pro");
        (await GetAsync(id)).ExamCount.Should().Be(1, "the exam followed the rename");
    }

    [Fact]
    public async Task Duplicates_AndBlankNames_AreRefused_AndAnUnusedMachineIsDeleted()
    {
        var (brand, model) = (Unique("Brand"), Unique("Model"));
        var id = await AddAsync(brand, model);

        var duplicate = await _supervisor.PostAsJsonAsync("/api/lab-machines", new LabMachineRequest { Brand = brand.ToUpperInvariant(), Model = $" {model} " });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await duplicate.Content.ReadAsStringAsync()).Should().Contain("already on the list");
        (await _supervisor.PostAsJsonAsync("/api/lab-machines", new LabMachineRequest { Brand = "  ", Model = "X" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _supervisor.DeleteAsync($"/api/lab-machines/{id}")).EnsureSuccessStatusCode();
        (await _supervisor.GetFromJsonAsync<List<LabMachineDto>>("/api/lab-machines"))!.Should().NotContain(m => m.Id == id);
        (await _supervisor.DeleteAsync($"/api/lab-machines/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LabStaff_SeeTheList_OnlySupervisorsChangeIt()
    {
        var lab = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.LABORATORY)]);

        (await lab.GetAsync("/api/lab-machines")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await lab.PostAsJsonAsync("/api/lab-machines", new LabMachineRequest { Brand = "A", Model = "B" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
