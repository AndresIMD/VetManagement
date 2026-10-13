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
        report.Sections.DirectionArrows.Should().BeTrue("templates print the arrows unless turned off");
    }

    [Fact]
    public async Task ArrowsTurnedOffInTheTemplate_AreOffInTheIssuedReport()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        var template = LipaseTemplate(examId);
        template.Sections.DirectionArrows = false;
        await AddTemplateAsync(template);

        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 12), Values = new Dictionary<string, string> { ["cpl"] = "86.6" } });

        report.Sections.DirectionArrows.Should().BeFalse();
    }

    private static ExamReportTemplate CortisolTemplate(int examId) => new()
    {
        ExamId = examId,
        Title = "SUPRESIÓN CON DEXAMETASONA",
        Parameters =
        [
            new ReportParameter { Code = "basal", Name = "Cortisol basal", Unit = "µg/dL" },
            new ReportParameter { Code = "4h", Name = "Cortisol 4 h", Unit = "µg/dL" },
            new ReportParameter { Code = "8h", Name = "Cortisol 8 h", Unit = "µg/dL" }
        ],
        Rules =
        [
            new InterpretationRule { Label = "Supresión normal", Conditions = [new RuleCondition { Code = "8h", Operator = RuleOperator.LessThan, Value = 1.4m }] },
            new InterpretationRule
            {
                Label = "Compatible con origen hipofisario",
                Conditions =
                [
                    new RuleCondition { Code = "8h", Operator = RuleOperator.GreaterOrEqual, Value = 1.4m },
                    new RuleCondition { Code = "4h", Operator = RuleOperator.LessThan, Value = 50, PercentOf = "basal" }
                ]
            },
            new InterpretationRule { Label = "Sin supresión", Conditions = [new RuleCondition { Code = "8h", Operator = RuleOperator.GreaterOrEqual, Value = 1.4m }] }
        ]
    };

    [Theory]
    [InlineData("6.0", "1.0", "1.2", "Supresión normal")]
    [InlineData("6.0", "2.9", "2.0", "Compatible con origen hipofisario")]
    [InlineData("6.0", "3.0", "2.0", "Sin supresión")] // 3.0 is exactly 50 % of basal: not below it
    public async Task Rules_InterpretSeveralValuesTogether_FirstMatchingRuleWins(string basal, string fourHours, string eightHours, string expected)
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(CortisolTemplate(examId));

        var report = await CreateAsync(_employee, new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 12),
            Values = new Dictionary<string, string> { ["basal"] = basal, ["4h"] = fourHours, ["8h"] = eightHours }
        });

        report.Interpretation.Should().Be(expected);
    }

    [Fact]
    public async Task Rules_UsingUnknownParameters_AreRejected()
    {
        var (_, examId) = await AddPetAndExamAsync(Species.Dog);
        var template = CortisolTemplate(examId);
        template.Rules[0].Conditions[0].Code = "12h";

        var errors = template.Validate();

        errors.Should().ContainSingle().Which.Should().Contain("rule 1");
    }

    [Fact]
    public async Task PreviousResultColumn_CopiesThePetsLatestEarlierReportOfTheSameExam()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        var template = LipaseTemplate(examId);
        template.Sections.PreviousResult = true;
        await AddTemplateAsync(template);
        Task<ExamReportDto> Issue(DateOnly on, string value) => CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = on, Values = new Dictionary<string, string> { ["cpl"] = value } });

        var first = await Issue(new DateOnly(2026, 8, 1), "150");
        await Issue(new DateOnly(2026, 9, 1), "250");
        var latest = await Issue(new DateOnly(2026, 10, 1), "420");

        first.PreviousReceivedOn.Should().BeNull("there was nothing before it");
        latest.PreviousReceivedOn.Should().Be(new DateOnly(2026, 9, 1));
        latest.Results.Single().PreviousValue.Should().Be("250");

        var corrected = await _employee.PutAsJsonAsync($"/api/exam-reports/{first.Id}",
            new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 8, 1), Values = new Dictionary<string, string> { ["cpl"] = "155" } });
        corrected.EnsureSuccessStatusCode();
        (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{latest.Id}"))!.Results.Single().PreviousValue
            .Should().Be("250", "the copy is a snapshot: correcting an older report doesn't change a later one");
    }

    [Fact]
    public async Task TypedPatient_IsKeptInTheReport_EvaluatedForItsSpecies_AndListed()
    {
        var (_, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(new ExamReportTemplate
        {
            ExamId = examId,
            Title = "POTASIO",
            Sections = new ExamReportSections { PreviousResult = true },
            Parameters = [new ReportParameter { Code = "k", Name = "K+", Ranges = [new ReferenceRange { Species = Species.Cat, Min = 2.9m, Max = 4.2m }] }]
        });

        var report = await CreateAsync(_employee, new
        {
            ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 13), Values = new Dictionary<string, string> { ["k"] = "4.5" },
            Patient = new { Name = " Michi ", Species = Species.Cat, Sex = Sex.Female, Birthdate = new DateOnly(2022, 1, 5), OwnerName = "Ana", OwnerLastName = "Pérez" }
        });

        report.PetId.Should().BeNull();
        report.Should().BeEquivalentTo(new { PetName = "Michi", Species = Species.Cat, Sex = Sex.Female, OwnerName = "Ana Pérez", OwnerLastName = "Pérez" });
        report.Birthdate.Should().Be(new DateTime(2022, 1, 5));
        report.Results.Single().Direction.Should().Be(ResultDirection.High, "the typed species picks the cat's range");
        report.PreviousReceivedOn.Should().BeNull("a typed patient has no history");
        (await _admin.GetFromJsonAsync<List<ExamReportDto>>("/api/exam-reports"))!.Should().Contain(r => r.Id == report.Id);
    }

    [Fact]
    public async Task ReportWithoutPetOrTypedPatient_IsRejected()
    {
        var (_, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(LipaseTemplate(examId));

        var response = await _employee.PostAsJsonAsync("/api/exam-reports", new { ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 13), Values = new Dictionary<string, string> { ["cpl"] = "1" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("type the patient's name");
    }

    [Fact]
    public async Task TextResults_AreFlaggedOnlyWhenTheOptionIsMarkedAbnormal()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        string[] options = ["Positivo", "Negativo"];
        await AddTemplateAsync(new ExamReportTemplate
        {
            ExamId = examId,
            Title = "SNAP 4Dx",
            Parameters =
            [
                new ReportParameter { Code = "ehr", Name = "Ehrlichia", Kind = ReportParameterKind.Text, Options = [.. options], AbnormalOptions = ["Positivo"] },
                new ReportParameter { Code = "diro", Name = "Dirofilaria", Kind = ReportParameterKind.Text, Options = [.. options], AbnormalOptions = ["Positivo"] },
                new ReportParameter { Code = "dea", Name = "DEA 1", Kind = ReportParameterKind.Text, Options = ["DEA 1 Positivo", "DEA 1 Negativo"] }
            ]
        });

        var report = await CreateAsync(_employee, new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 13),
            Values = new Dictionary<string, string> { ["ehr"] = "positivo", ["diro"] = "Negativo", ["dea"] = "DEA 1 Positivo" }
        });

        report.Results.Should().AllSatisfy(r => r.Kind.Should().Be(ReportParameterKind.Text));
        report.Results.Select(r => r.Abnormal).Should().Equal(true, false, false);
    }

    [Theory]
    [InlineData(30, 80, true)]
    [InlineData(9, 80, false)]
    [InlineData(30, 151, false)]
    public void LogoSize_MustStayWithinThePrintableLimits(int heightMm, int widthMm, bool valid)
    {
        var settings = new ExamReportSettings { LogoHeightMm = heightMm, LogoWidthMm = widthMm };

        settings.Validate().Should().HaveCount(valid ? 0 : 1);
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
