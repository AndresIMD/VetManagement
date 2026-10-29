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
using VetManagement.Domain.Configuration;
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
    public async Task SectionTitles_PrintAboveTheirOwnResults_AndOnlyWhenOneOfThemHasAValue()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        static ReportParameter Section(string code, string name) => new() { Code = code, Name = name, Kind = ReportParameterKind.Section };
        static ReportParameter Value(string code) => new() { Code = code, Name = code, Unit = "mmHg" };
        await AddTemplateAsync(new ExamReportTemplate
        {
            ExamId = examId, Title = "PRUEBA GASES EN SANGRE",
            Parameters = [Section("gas", "VALOR DE LA SANGRE Y EL GAS"), Value("ph"), Value("pco2"), Section("oxi", "VALORES DE OXIMETRÍA"), Value("hct"),
                Section("met", "VALOR DE LOS METABOLITOS"), Value("lac")]
        });

        var report = await CreateAsync(_employee, new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 21),
            Values = new Dictionary<string, string> { ["ph"] = "7.33", ["pco2"] = "38.8", ["lac"] = "1.22" }
        });

        report.Results.Select(r => (r.Kind, r.Name)).Should().Equal(
            (ReportParameterKind.Section, "VALOR DE LA SANGRE Y EL GAS"), (ReportParameterKind.Number, "ph"), (ReportParameterKind.Number, "pco2"),
            (ReportParameterKind.Section, "VALOR DE LOS METABOLITOS"), (ReportParameterKind.Number, "lac"));

        var valueForATitle = await _employee.PostAsJsonAsync("/api/exam-reports", new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 21), Values = new Dictionary<string, string> { ["oxi"] = "1" }
        });
        valueForATitle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PageBreak_IsKeptInTheIssuedReport_AndTakesNoValue()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(new ExamReportTemplate
        {
            ExamId = examId, Title = "PRUEBA PERFIL",
            Parameters = [new ReportParameter { Code = "alt", Name = "ALT" }, new ReportParameter { Code = "page-break", Kind = ReportParameterKind.PageBreak },
                new ReportParameter { Code = "crea", Name = "CREA" }]
        });

        var report = await CreateAsync(_employee, new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 23),
            Values = new Dictionary<string, string> { ["alt"] = "45", ["crea"] = "1.2" }
        });
        report.Results.Select(r => r.Kind).Should().Equal(ReportParameterKind.Number, ReportParameterKind.PageBreak, ReportParameterKind.Number);

        var valueForABreak = await _employee.PostAsJsonAsync("/api/exam-reports", new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 23), Values = new Dictionary<string, string> { ["page-break"] = "1" }
        });
        valueForABreak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void PageBreaks_AreKeptOnlyBetweenValues_OnceAndBeforeAWaitingTitle()
    {
        static ReportParameter Break(string code) => new() { Code = code, Kind = ReportParameterKind.PageBreak };
        static ReportParameter Section(string code) => new() { Code = code, Name = code.ToUpperInvariant(), Kind = ReportParameterKind.Section };
        static ReportParameter Value(string code) => new() { Code = code, Name = code };
        var template = new ExamReportTemplate
        {
            ExamId = 1, Title = "X",
            Parameters = [Break("pb-start"), Value("a"), Break("pb-1"), Break("pb-2"), Section("s"), Value("b"), Section("t"), Break("pb-3"), Value("c"), Break("pb-end")]
        };

        template.ResultsFor(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2", ["c"] = "3" }, Species.Dog)
            .Select(r => (r.Kind, r.Code)).Should().Equal(
                (ReportParameterKind.Number, "a"), (ReportParameterKind.PageBreak, "pb-1"), (ReportParameterKind.Section, "s"), (ReportParameterKind.Number, "b"),
                (ReportParameterKind.PageBreak, "pb-3"), (ReportParameterKind.Section, "t"), (ReportParameterKind.Number, "c"));
    }

    [Fact]
    public void PageBreak_NeedsNoName_ButIsNotAParameter()
    {
        var pageBreak = new ReportParameter { Code = "page-break", Kind = ReportParameterKind.PageBreak };

        new ExamReportTemplate { ExamId = 1, Title = "X", Parameters = [pageBreak] }
            .Validate().Should().ContainSingle().Which.Should().Contain("at least one parameter");
        new ExamReportTemplate { ExamId = 1, Title = "X", Parameters = [new ReportParameter { Code = "a", Name = "A" }, pageBreak] }
            .Validate().Should().BeEmpty();
    }

    [Fact]
    public void SectionTitles_FollowThePatientBar_UntilTheyGetAColourOfTheirOwn()
    {
        var settings = SettingsJson.Deserialize<ExamReportSettings>("""{ "headerColor": "#112233" }""");
        settings.SectionBarColor.Should().Be("#112233");

        settings.SectionColor = "#445566";
        settings.SectionBarColor.Should().Be("#445566");
        settings.Validate().Should().BeEmpty();

        settings.SectionColor = "blue";
        settings.Validate().Should().ContainSingle().Which.Should().Contain("#RRGGBB");
    }

    [Theory]
    [InlineData("#2D2E30", "#FFFFFF")] // the default dark bar
    [InlineData("#12A9C1", "#FFFFFF")] // a brand colour of medium tone, white like the result bar
    [InlineData("#8E24AA", "#FFFFFF")]
    [InlineData("#E0E0E0", "#222222")] // light grey
    [InlineData("#FFEB3B", "#222222")] // yellow
    [InlineData("#FF9800", "#222222")] // orange
    [InlineData("blue", "#FFFFFF")]    // not a colour: as before
    public void SectionTitles_GetDarkText_OnALightColour(string background, string text)
        => ExamReportSettings.TextColorOn(background).Should().Be(text);

    [Fact]
    public void Template_WithOnlySectionTitles_HasNoParameters()
        => new ExamReportTemplate
        {
            ExamId = 1, Title = "X", Parameters = [new ReportParameter { Code = "a", Name = "A", Kind = ReportParameterKind.Section }]
        }.Validate().Should().ContainSingle().Which.Should().Contain("at least one parameter");

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

        // By code: the in-memory test database doesn't keep the order of a report's lines after other reports are deleted.
        ExamReportResultDto Line(string code) => report.Results.Single(r => r.Code == code);
        Line("k").Should().BeEquivalentTo(new { Reference = "2.9 - 4.2", Abnormal = true, Direction = ResultDirection.High, BarMin = 2.9m, BarMax = 4.2m },
            "the cat's own range replaces the one for all species");
        Line("na").Should().BeEquivalentTo(new { Reference = "147 - 162", Abnormal = true, Direction = ResultDirection.Low, BarMin = (decimal?)null },
            "no bar unless the parameter asks for one");
        Line("nak").Should().BeEquivalentTo(new { Reference = "Sin rango de referencia", Abnormal = false, Direction = ResultDirection.None });
        Line("sample").Should().BeEquivalentTo(new { Value = "Sangre venosa", Abnormal = false });
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
    public async Task CriteriaTable_IsCopiedIntoTheReport_OnlyWhenTurnedOn()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        var template = CortisolTemplate(examId);
        template.Criteria = new CriteriaTable
        {
            Title = "Supresión con dexametasona",
            Columns = ["Cortisol 4 h", "Cortisol 8 h", "Interpretación"],
            Rows = [new CriteriaRow { Cells = ["-", "< 1 µg/dL", "Normal"] }, new CriteriaRow { Cells = ["1 - 1.4 µg/dL", "1 - 1.4 µg/dL", "Equívoco"] }]
        };
        template.Sections.CriteriaTable = true;
        await AddTemplateAsync(template);

        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), Values = new Dictionary<string, string> { ["basal"] = "5" } });

        report.Sections.CriteriaTable.Should().BeTrue();
        report.Criteria.Should().BeEquivalentTo(new CriteriaTableDto
        {
            Title = "Supresión con dexametasona",
            Columns = ["Cortisol 4 h", "Cortisol 8 h", "Interpretación"],
            Rows = [["-", "< 1 µg/dL", "Normal"], ["1 - 1.4 µg/dL", "1 - 1.4 µg/dL", "Equívoco"]]
        }, o => o.WithStrictOrdering());

        template.Sections.CriteriaTable = false;
        template.Validate().Should().BeEmpty("a table kept while turned off is fine");
        template.Criteria.Rows[0].Cells.RemoveAt(0);
        template.Validate().Should().ContainSingle().Which.Should().Contain("one cell per column");
    }

    [Fact]
    public async Task CriteriaTable_TurnedOff_IsNotCopied()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        var template = CortisolTemplate(examId);
        template.Criteria = new CriteriaTable { Columns = ["A"], Rows = [new CriteriaRow { Cells = ["x"] }] };
        await AddTemplateAsync(template);

        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), Values = new Dictionary<string, string> { ["basal"] = "5" } });

        report.Criteria.Should().BeNull();
    }

    [Fact]
    public async Task CriteriaTable_Headers_AreKeptInTheTemplate_AndCopiedIntoTheReport()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        var template = CortisolTemplate(examId);
        template.Criteria = new CriteriaTable
        {
            Headers = CriteriaHeaders.Both,
            Columns = ["Peso / Edad", "< 1 año", "≥ 1 año"],
            Rows = [new CriteriaRow { Cells = ["< 10 kg", "1 - 5", "2 - 6"] }]
        };
        template.Sections.CriteriaTable = true;
        await AddTemplateAsync(template);

        var document = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!.FindTemplate(examId)!.Criteria.Headers.Should().Be(CriteriaHeaders.Both);
        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 3), Values = new Dictionary<string, string> { ["basal"] = "5" } });

        report.Criteria!.Headers.Should().Be(CriteriaHeaders.Both);
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
            Sections = new ExamReportSections { PreviousResult = true, LotNumber = true },
            Parameters = [new ReportParameter { Code = "k", Name = "K+", Ranges = [new ReferenceRange { Species = Species.Cat, Min = 2.9m, Max = 4.2m }] }]
        });

        var report = await CreateAsync(_employee, new
        {
            ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 13), Values = new Dictionary<string, string> { ["k"] = "4.5" },
            Patient = new { Name = " Michi ", Species = Species.Cat, Sex = Sex.Female, Birthdate = new DateOnly(2022, 1, 5), BirthdateApproximate = true, OwnerName = "Ana", OwnerLastName = "Pérez" },
            ReferringVet = "dra. soto", LotNumber = " a123 "
        });

        report.PetId.Should().BeNull();
        report.Should().BeEquivalentTo(new { PetName = "MICHI", Species = Species.Cat, Sex = Sex.Female, OwnerName = "ANA PÉREZ", OwnerLastName = "PÉREZ", ReferringVet = "DRA. SOTO", LotNumber = "A123" },
            "typed report data is stored in capitals, as it prints");
        report.Birthdate.Should().Be(new DateTime(2022, 1, 5));
        report.Patient!.BirthdateApproximate.Should().BeTrue("the form shows an approximate age again on correction");
        report.Sections.LotNumber.Should().BeTrue("the form asks for the lot when the template does");
        report.Results.Single().Direction.Should().Be(ResultDirection.High, "the typed species picks the cat's range");
        report.PreviousReceivedOn.Should().BeNull("a typed patient has no history");
        (await _admin.GetFromJsonAsync<List<ExamReportDto>>("/api/exam-reports"))!.Should().Contain(r => r.Id == report.Id);
    }

    [Fact]
    public async Task TypedPatient_IsAHiddenProvisionalClient_SharingHistory_UntilRegistered()
    {
        var (_, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(new ExamReportTemplate
        {
            ExamId = examId,
            Title = "GLUCOSA",
            Sections = new ExamReportSections { PreviousResult = true },
            Parameters = [new ReportParameter { Code = "glu", Name = "Glucosa" }]
        });
        object Request(DateOnly on, string value) => new
        {
            ExamId = examId, ReceivedOn = on, Values = new Dictionary<string, string> { ["glu"] = value },
            Patient = new { Name = "Toby", Species = Species.Dog, OwnerName = "Luis", OwnerLastName = "Rojas" }
        };
        async Task<string> ClientsJson() => await _admin.GetStringAsync("/api/clientpet/clients");

        var first = await CreateAsync(_employee, Request(new DateOnly(2026, 9, 1), "90"));
        var second = await CreateAsync(_employee, Request(new DateOnly(2026, 10, 1), "130"));

        second.PreviousReceivedOn.Should().Be(new DateOnly(2026, 9, 1), "the same typed patient shares its history");
        second.Results.Single().PreviousValue.Should().Be("90");
        second.PetId.Should().BeNull("the provisional pet stays internal");
        second.CanRegisterPatient.Should().BeTrue();
        (await ClientsJson()).Should().NotContain("ROJAS", "provisional clients are hidden from client lists");
        (await _employee.GetFromJsonAsync<List<ExamReportPatientDto>>("/api/exam-reports/provisional-patients?search=tob"))!
            .Should().ContainSingle().Which.OwnerLastName.Should().Be("ROJAS");

        var examsOnly = _factory.CreateClientWithPermissions([Permissions.EXAMS_PERFORMED.READ, Permissions.EXAMS_PERFORMED.UPDATE]);
        (await examsOnly.PostAsync($"/api/exam-reports/{second.Id}/register-patient", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "registering creates a client");
        var response = await _employee.PostAsync($"/api/exam-reports/{second.Id}/register-patient", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ClientsJson()).Should().Contain("ROJAS");
        var registered = (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{first.Id}"))!;
        registered.PetId.Should().NotBeNull("both reports now belong to the registered pet");
        registered.Patient.Should().BeNull();
        registered.PetName.Should().Be("TOBY");
    }

    [Fact]
    public async Task EveryChange_ToReportsImagesAndDesign_IsAudited()
    {
        var (petId, examId) = await AddPetAndExamAsync(Species.Dog);
        await AddTemplateAsync(LipaseTemplate(examId)); // design + templates: settings save
        var report = await CreateAsync(_employee, new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 13), Values = new Dictionary<string, string> { ["cpl"] = "80" } });
        (await _employee.PutAsJsonAsync($"/api/exam-reports/{report.Id}",
            new { PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 13), Values = new Dictionary<string, string> { ["cpl"] = "90" } })).EnsureSuccessStatusCode();
        (await _admin.PutAsJsonAsync("/api/exam-reports/images/ReportLogo", new { ContentType = "image/png", Data = new byte[] { 1, 2, 3 } })).EnsureSuccessStatusCode();
        (await _admin.DeleteAsync("/api/exam-reports/images/ReportLogo")).EnsureSuccessStatusCode();
        (await _admin.DeleteAsync($"/api/exam-reports/{report.Id}")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var logs = scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.ToList();
        logs.Where(l => l.EntityName == nameof(ExamReport) && l.EntityId == report.Id).Select(l => l.Action)
            .Should().Equal(AuditActionType.Add, AuditActionType.Edit, AuditActionType.Delete);
        logs.Where(l => l.EntityName == nameof(ExamReport) && l.Action == AuditActionType.Edit && l.EntityId == report.Id)
            .Should().ContainSingle().Which.Changes.Should().Contain("Before:").And.Contain("After:");
        logs.Should().Contain(l => l.EntityName == nameof(ClinicImage) && l.Action == AuditActionType.Edit)
            .And.Contain(l => l.EntityName == nameof(ClinicImage) && l.Action == AuditActionType.Delete)
            .And.Contain(l => l.EntityName == nameof(ExamReportSettings) && l.Action == AuditActionType.Edit);
        logs.Where(l => l.EntityName == nameof(ExamReport)).Should().OnlyContain(l => !string.IsNullOrEmpty(l.User), "every entry names who did it");
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

    [Theory]
    [InlineData(10.5, 10.5, true)]
    [InlineData(8, 14, true)]
    [InlineData(7.5, 10.5, false)]
    [InlineData(10.5, 14.5, false)]
    public void TextSizes_OfPatientDataAndResults_StayReadable(double patientPt, double resultsPt, bool valid)
    {
        var settings = new ExamReportSettings { PatientFontPt = patientPt, ResultsFontPt = resultsPt };

        settings.Validate().Should().HaveCount(valid ? 0 : 1);
    }

    [Fact]
    public void Design_SavedBeforeTextSizes_PrintsAtTheUsualSize()
    {
        var settings = SettingsJson.Deserialize<ExamReportSettings>("""{ "fontFamily": "Arial" }""");

        settings.PatientFontPt.Should().Be(ExamReportSettings.DefaultFontPt);
        settings.ResultsFontPt.Should().Be(ExamReportSettings.DefaultFontPt);
    }

    [Fact]
    public void Design_SavedBeforeTheOption_KeepsThePatientDataOnLaterPages()
    {
        SettingsJson.Deserialize<ExamReportSettings>("""{ "fontFamily": "Arial" }""").PatientDataOnLaterPages.Should().BeTrue();
        SettingsJson.Deserialize<ExamReportSettings>("""{ "patientDataOnLaterPages": false }""").PatientDataOnLaterPages.Should().BeFalse();
    }

    [Fact]
    public void FooterMarkup_KeepsGaps_AndPrintsDoubleAsterisksInBold()
    {
        ReportText.Lines("\r\n**Clínica Demo**\r\nRUT: 11.111.111-1\r\n\r\n  Calle 1  \n\n").Should().Equal("**Clínica Demo**", "RUT: 11.111.111-1", "", "Calle 1");
        ReportText.Lines("  ").Should().BeEmpty();

        ReportText.Spans("Fono: **+56 9 1234 5678** - **24 h**").Should().Equal(
            new ReportTextSpan("Fono: ", false), new ReportTextSpan("+56 9 1234 5678", true), new ReportTextSpan(" - ", false), new ReportTextSpan("24 h", true));
        ReportText.Spans("5 ** 2").Should().Equal(new ReportTextSpan("5 ", false), new ReportTextSpan("** 2", false));
    }

    [Theory]
    [InlineData("EXAMEN Gases en sangre", "EXAMEN GASES EN SANGRE")]
    [InlineData("PRUEBA Coagulación", "PRUEBA COAGULACIÓN")]
    [InlineData(" Panel de coagulación ", "Panel de coagulación")]
    [InlineData("T4 total", "T4 total")]
    [InlineData("", "")]
    public void Title_StartingInCapitals_PrintsAllInCapitals(string title, string printed)
        => ReportText.Title(title).Should().Be(printed);

    [Fact]
    public void Footer_SavedAsLines_IsReadAsAParagraph_WithTheNameInBold()
    {
        var settings = SettingsJson.Deserialize<ExamReportSettings>("""{ "footerLines": ["Clínica Demo", "Calle 1"] }""");

        settings.Footer.Should().Be("**Clínica Demo**\nCalle 1");
        SettingsJson.Serialize(settings).Should().NotContain("footerLines");
        SettingsJson.Deserialize<ExamReportSettings>("""{ "footer": "Nueva", "footerLines": ["Vieja"] }""").Footer.Should().Be("Nueva");

        settings.Footer = string.Join("\n", Enumerable.Repeat("x", ExamReportSettings.MaxFooterLines + 1));
        settings.Validate().Should().ContainSingle().Which.Should().Contain("footer lines");
    }

    [Fact]
    public async Task Footer_KeepsItsLineBreaks_AndBold_WhenSaved()
    {
        const string footer = "**CLÍNICA DEMO**\nRUT: 11.111.111-1\n\nEl resultado no es un diagnóstico.\nFono: **+56 9 1234 5678**";
        var document = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        var settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!;
        settings.Footer = footer;

        (await _admin.PutAsJsonAsync("/api/exam-reports/settings", new ExamReportSettingsDocument
        {
            Version = document.Version,
            Settings = JsonSerializer.SerializeToElement(settings, SettingsJson.Options)
        })).EnsureSuccessStatusCode();

        var saved = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        saved.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!.Footer.Should().Be(footer);
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

        // Revalidated on each use: unchanged, the browser keeps its copy (304, no body).
        served.Headers.CacheControl!.NoCache.Should().BeTrue();
        using var again = new HttpRequestMessage(HttpMethod.Get, "/api/exam-reports/images/ReportLogo");
        again.Headers.IfNoneMatch.Add(served.Headers.ETag!);
        (await _employee.SendAsync(again)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Theory]
    [InlineData("< 1.0", 1.0)]
    [InlineData(">99", 99.0)]
    [InlineData("86,6", 86.6)]
    [InlineData("n/a", null)]
    public void ParseNumber_ReadsAnalyserBoundsAndDecimalCommas(string text, double? expected)
        => ReportParameter.ParseNumber(text).Should().Be(expected is null ? null : (decimal)expected);
}
