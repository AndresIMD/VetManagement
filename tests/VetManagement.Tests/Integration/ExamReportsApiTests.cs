using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Configuration;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>Exam reports: values are interpreted with the exam's template bands, and the result is a snapshot.</summary>
public class ExamReportsApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _employee;

    public ExamReportsApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _employee = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.LABORATORY)]);
    }

    private async Task<(int PetId, int ExamId)> AddPetAndExamAsync(Species species)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = new Client("Ana", "López", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, "ana@example.test");
        db.Clients.Add(owner);
        var exam = new Exam { Name = $"cPL {Guid.NewGuid():N}", Brand = "BioNote", Machine = "Vcheck V200" };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        var pet = new Pet { OwnerId = owner.Id, Name = "Gala", Breed = "Bulldog francés", Species = species };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        return (pet.Id, exam.Id);
    }

    private static ExamReportTemplate LipaseTemplate(int examId) => new()
    {
        ExamId = examId,
        Title = "PRUEBA cPL",
        Parameters =
        [
            new ReportParameter
            {
                Code = "cpl", Name = "cPL", Unit = "ng/mL",
                Bands =
                [
                    new InterpretationBand { Max = 200, Label = "Normal" },
                    new InterpretationBand { Min = 200, Max = 399.9m, Label = "Zona gris", Abnormal = true },
                    new InterpretationBand { Min = 400, Label = "Consistente con pancreatitis", Abnormal = true }
                ]
            }
        ]
    };

    private async Task AddTemplateAsync(ExamReportTemplate template)
    {
        var document = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        var settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!;
        settings.Templates.Add(template);
        var response = await _admin.PutAsJsonAsync("/api/exam-reports/settings", new ExamReportSettingsDocument
        {
            Version = document.Version,
            Settings = JsonSerializer.SerializeToElement(settings, SettingsJson.Options)
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<ExamReportDto> CreateAsync(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync("/api/exam-reports", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var id = (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        return (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{id}"))!;
    }

    [Theory]
    [InlineData("86.6", "Normal", false)]
    [InlineData("250", "Zona gris", true)]
    [InlineData("450", "Consistente con pancreatitis", true)]
    public async Task Report_IsInterpretedWithTheTemplateBands(string value, string expected, bool abnormal)
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(LipaseTemplate(examId));

        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), ReferringVet = "Carmen Morales", Values = new Dictionary<string, string> { ["cpl"] = value } });

        report.Title.Should().Be("PRUEBA cPL");
        report.Method.Should().Be("BioNote - Vcheck V200");
        report.PetName.Should().Be("Gala");
        report.OwnerName.Should().Be("Ana López");
        var line = report.Results.Single();
        line.Band.Should().Be(expected);
        line.Abnormal.Should().Be(abnormal);
        line.Reference.Should().Be("≤ 200 Normal / 200 - 399.9 Zona gris / ≥ 400 Consistente con pancreatitis");
        report.Interpretation.Should().Be(expected);
    }

    [Fact]
    public async Task WrittenInterpretation_ReplacesTheAutomaticOne_AndTemplateChangesDontAlterIssuedReports()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(LipaseTemplate(examId));

        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), Values = new Dictionary<string, string> { ["cpl"] = "86.6" }, Interpretation = "Sin hallazgos." });
        report.Interpretation.Should().Be("Sin hallazgos.");

        var document = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        var settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!;
        settings.FindTemplate(examId)!.Title = "LIPASA PANCREÁTICA";
        (await _admin.PutAsJsonAsync("/api/exam-reports/settings", new ExamReportSettingsDocument
        {
            Version = document.Version,
            Settings = JsonSerializer.SerializeToElement(settings, SettingsJson.Options)
        })).EnsureSuccessStatusCode();

        (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{report.Id}"))!.Title.Should().Be("PRUEBA cPL");
    }

    [Fact]
    public async Task SpeciesRanges_MarkHighAndLow_FeedTheBar_AndAWrittenReferenceIsKept()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Cat);
        await AddTemplateAsync(new ExamReportTemplate
        {
            ExamId = examId,
            Title = "GASES SANGUÍNEOS",
            Parameters =
            [
                new ReportParameter
                {
                    Code = "k", Name = "K+", Unit = "mmol/L", ShowBar = true,
                    Ranges = [new ReferenceRange { Min = 3.4m, Max = 4.9m }, new ReferenceRange { Species = Species.Cat, Min = 2.9m, Max = 4.2m }]
                },
                new ReportParameter { Code = "na", Name = "Na+", Unit = "mmol/L", Ranges = [new ReferenceRange { Species = Species.Cat, Min = 147, Max = 162 }] },
                new ReportParameter { Code = "nak", Name = "Na/K", Reference = ReferenceDisplay.Text, ReferenceText = "Sin rango de referencia" },
                new ReportParameter { Code = "sample", Name = "Muestra", Kind = ReportParameterKind.Text }
            ]
        });

        var report = await CreateAsync(_employee, new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 5),
            Values = new Dictionary<string, string> { ["k"] = "4,4", ["na"] = "140", ["nak"] = "38", ["sample"] = "Sangre venosa" }
        });

        report.Results[0].Should().BeEquivalentTo(new { Reference = "2.9 - 4.2", Abnormal = true, Direction = ResultDirection.High, BarMin = 2.9m, BarMax = 4.2m },
            "the cat's own range replaces the one for all species");
        report.Results[1].Should().BeEquivalentTo(new { Reference = "147 - 162", Abnormal = true, Direction = ResultDirection.Low, BarMin = (decimal?)null },
            "no bar unless the parameter asks for one");
        report.Results[2].Should().BeEquivalentTo(new { Reference = "Sin rango de referencia", Abnormal = false, Direction = ResultDirection.None });
        report.Results[3].Should().BeEquivalentTo(new { Value = "Sangre venosa", Abnormal = false });
        report.Interpretation.Should().BeNull();
    }

    [Fact]
    public async Task ExamWithoutTemplate_OrUnknownParameter_IsRejected()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);

        var response = await _employee.PostAsJsonAsync("/api/exam-reports", new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), Values = new Dictionary<string, string> { ["cpl"] = "1" } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("no report template");

        await AddTemplateAsync(LipaseTemplate(examId));
        response = await _employee.PostAsJsonAsync("/api/exam-reports", new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), Values = new Dictionary<string, string> { ["tsh"] = "1" } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Unknown parameters: tsh");
    }

    [Fact]
    public async Task Logo_IsStoredAndServed_AndOnlyManagersChangeIt()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        (await _employee.PutAsJsonAsync("/api/exam-reports/images/ReportLogo", new { ContentType = "image/png", Data = png }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _admin.PutAsJsonAsync("/api/exam-reports/images/ReportLogo", new { ContentType = "image/gif", Data = png }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PutAsJsonAsync("/api/exam-reports/images/ReportLogo", new { ContentType = "image/png", Data = png })).EnsureSuccessStatusCode();

        var served = await _employee.GetAsync("/api/exam-reports/images/ReportLogo");
        served.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await served.Content.ReadAsByteArrayAsync()).Should().Equal(png);
    }

    [Theory]
    [InlineData("< 1.0", 1.0)]
    [InlineData(">99", 99.0)]
    [InlineData("86,6", 86.6)]
    [InlineData("n/a", null)]
    public void ParseNumber_ReadsAnalyserBoundsAndDecimalCommas(string text, double? expected)
        => ReportParameter.ParseNumber(text).Should().Be(expected is null ? null : (decimal)expected);
}
