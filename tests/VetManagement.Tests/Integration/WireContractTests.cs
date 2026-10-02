using System.Text.Json;
using FluentAssertions;
using VetManagement.Contracts.Clients;
using VetManagement.Contracts.Audit;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Exams;
using VetManagement.Contracts.Medical;
using VetManagement.Domain.Enums;
using UiClient = VetManagement.Staff.UI.Models.Core.Client;
using UiPet = VetManagement.Staff.UI.Models.Core.Pet;
using UiExam = VetManagement.Staff.UI.Models.Exams.Exam;
using UiExamPerformed = VetManagement.Staff.UI.Models.Exams.ExamPerformed;
using UiExternalLab = VetManagement.Staff.UI.Models.Exams.ExternalLab;
using UiMedicalVisit = VetManagement.Staff.UI.Models.Medical.MedicalVisit;
using UiAuditLog = VetManagement.Staff.UI.Models.Audit.AuditLog;

namespace VetManagement.Tests.Integration;

/// <summary>
/// The staff UI posts its own view models and reads API DTOs. A renamed property on either side
/// would silently drop data, so this checks the JSON round-trip in both directions.
/// </summary>
public class WireContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static TTo RoundTrip<TTo>(object from) =>
        JsonSerializer.Deserialize<TTo>(JsonSerializer.Serialize(from, Web), Web)!;

    private static UiPet SamplePet() => new()
    {
        Id = 7, OwnerId = 3, Name = "Luna", Sex = Sex.Female, Species = Species.Cat, Breed = "Siamese",
        Birthdate = new DateTime(2020, 5, 1), Age = 5, Weight = 4.2f,
        ReproductiveStatus = ReproductiveStatus.Spayed, ChipId = 1234567
    };

    [Fact]
    public void UiClient_BindsTo_ClientRequest()
    {
        var ui = new UiClient("Ana", "Lopez", "TAX-1", "Main St 1", 912345678, "ana@mail.com");

        RoundTrip<ClientRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void UiPet_BindsTo_PetRequest()
    {
        var ui = SamplePet();

        RoundTrip<PetRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void ClientDto_ReadsInto_UiClient_WithPets()
    {
        var dto = new ClientDto
        {
            Id = 3, Name = "Ana", LastName = "Lopez", TaxId = "TAX-1", Address = "Main St 1",
            PhoneNumber = 912345678, Email = "ana@mail.com",
            Pets = [RoundTrip<PetDto>(SamplePet())]
        };

        RoundTrip<UiClient>(dto).Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void UiExam_BindsTo_ExamRequest()
    {
        var ui = new UiExam
        {
            Id = 4, Name = "Hemograma", Brand = "Idexx", Machine = "ProCyte", SampleType = SampleType.Blood,
            SampleContainer = SampleContainer.Tube, BuyPrice = 5000, SellPrice = 12000
        };

        RoundTrip<ExamRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void UiExternalLab_BindsTo_ExternalLabRequest()
    {
        var ui = SampleLab();

        RoundTrip<ExternalLabRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void UiExamPerformed_BindsTo_ExamPerformedRequest_WithItems()
    {
        var ui = new UiExamPerformed
        {
            Id = 9, PatientId = 7, Responsible = "dr.vet", Date = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            Items =
            [
                new() { Id = 1, ExamId = 4, Status = ExamItemStatus.InProgress, Attributes = { ["fasting"] = "yes" } },
                new() { Id = 0, ExamId = 5, IsExternal = true, ExternalEstimatedDays = 3, ExternalLabId = 2, ExternalLab = SampleLab() }
            ]
        };

        // The nested ExternalLab object is intentionally not part of the request (linked by id only).
        RoundTrip<ExamPerformedRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void ExamPerformedDto_ReadsInto_UiExamPerformed_WithLab()
    {
        var dto = new ExamPerformedDto
        {
            Id = 9, PatientId = 7, Responsible = "dr.vet", Date = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            Items =
            [
                new() { Id = 1, ExamId = 4, Status = ExamItemStatus.Completed, Attributes = { ["result"] = "ok" } },
                new() { Id = 2, ExamId = 5, IsExternal = true, ExternalLabId = 2, ExternalLab = RoundTrip<ExternalLabDto>(SampleLab()) }
            ]
        };

        RoundTrip<UiExamPerformed>(dto).Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void UiMedicalVisit_BindsTo_MedicalVisitRequest_WithProcedures()
    {
        var ui = SampleVisit();

        RoundTrip<MedicalVisitRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void MedicalVisitDto_ReadsInto_UiMedicalVisit()
    {
        var dto = RoundTrip<MedicalVisitDto>(SampleVisit());

        RoundTrip<UiMedicalVisit>(dto).Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void AuditPagedResponse_ReadsInto_UiPagedResult()
    {
        var response = new PagedResponse<AuditLogDto>(
            [new AuditLogDto { Id = 1, EntityId = 3, EntityName = "Client", Date = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Action = AuditActionType.Edit, Changes = "{}", User = "admin" }],
            TotalCount: 41, Page: 2, PageSize: 20);

        RoundTrip<VetManagement.Staff.UI.Models.DTOs.PagedResult<UiAuditLog>>(response).Should().BeEquivalentTo(response);
    }

    private static UiMedicalVisit SampleVisit() => new()
    {
        Id = 5, Date = new DateTime(2026, 1, 2, 9, 30, 0, DateTimeKind.Utc), PatientId = 7, RecordNumber = "R-1",
        PatientName = "Luna", Responsible = "dr.vet", Location = "Box 2", BudgetNumber = "B-9",
        PaymentStatus = PaymentStatus.Partial, PaymentMethod = PaymentMethod.Card, TotalValue = 35000,
        Procedures =
        [
            new() { Id = 1, ExamId = 4, Name = "Hemograma", Price = 12000, Notes = "fasting" },
            new() { Id = 0, Name = "Consulta", Price = 23000 }
        ]
    };

    private static UiExternalLab SampleLab() => new()
    {
        Id = 2, Name = "LabVet", ContactName = "Eva", Phone = "+56 9 1234", Email = "lab@vet.cl",
        Address = "Av. 1", Notes = "24h", DefaultTurnaroundDays = 3
    };
}
