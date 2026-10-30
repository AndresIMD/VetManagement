using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Exams;

/// <summary>
/// Creates or corrects a report. Values are keyed by parameter code; names, units, references and the
/// interpretation come from the exam's report template.
/// </summary>
public sealed class ExamReportRequest
{
    /// <summary>A registered pet; leave empty and fill <see cref="Patient"/> for an animal that isn't registered.</summary>
    [Range(1, int.MaxValue)]
    public int? PetId { get; init; }

    public ExamReportPatientDto? Patient { get; init; }

    [Range(1, int.MaxValue)]
    public int ExamId { get; init; }

    public DateOnly ReceivedOn { get; init; }

    [MaxLength(200)]
    public string? ReferringVet { get; init; }

    /// <summary>Lot of the test kit or reagent; optional.</summary>
    [MaxLength(50)]
    public string? LotNumber { get; init; }

    public Dictionary<string, string> Values { get; init; } = [];

    /// <summary>Written interpretation; leave empty to use the one the bands give.</summary>
    [MaxLength(2000)]
    public string? Interpretation { get; init; }
}

/// <summary>Patient and owner typed into a report; kept only in that report.</summary>
public sealed class ExamReportPatientDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public Species Species { get; set; }
    [MaxLength(100)]
    public string? Breed { get; set; }
    public Sex Sex { get; set; }
    public DateOnly? Birthdate { get; set; }
    /// <summary>Birthdate comes from an approximate age; the form shows it as years and months again.</summary>
    public bool BirthdateApproximate { get; set; }
    [MaxLength(100)]
    public string? OwnerName { get; set; }
    [MaxLength(100)]
    public string? OwnerLastName { get; set; }
}

public sealed class ExamReportDto
{
    public int Id { get; set; }
    /// <summary>Null when the patient was typed into the report (see <see cref="Patient"/>).</summary>
    public int? PetId { get; set; }
    public ExamReportPatientDto? Patient { get; set; }
    /// <summary>The typed patient is kept as a provisional client and pet that can be registered (see the register-patient endpoint).</summary>
    public bool CanRegisterPatient { get; set; }
    public int ExamId { get; set; }
    /// <summary>Catalog name of the exam (for the PDF file name); filled when one report is read.</summary>
    public string? ExamName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Method { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public string? ReferringVet { get; set; }
    public string? LotNumber { get; set; }
    public string? Interpretation { get; set; }
    public ExamReportSectionsDto Sections { get; set; } = new();
    public List<ExamReportResultDto> Results { get; set; } = [];
    public CriteriaTableDto? Criteria { get; set; }
    /// <summary>Date of the previous report whose values are in <see cref="ExamReportResultDto.PreviousValue"/>.</summary>
    public DateOnly? PreviousReceivedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    public string PetName { get; set; } = string.Empty;
    public Species Species { get; set; }
    public string? Breed { get; set; }
    public Sex Sex { get; set; }
    public DateTime? Birthdate { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerLastName { get; set; }

    /// <summary>The exam's inventory product the report can still deduct; null when none, already deducted or Inventory is off.</summary>
    public ExamReportStockDto? StockToDeduct { get; set; }
    /// <summary>What the report took from inventory; null when nothing (or it was returned).</summary>
    public ExamReportStockDto? StockDeducted { get; set; }
}

/// <summary>An inventory product and quantity for a report: what it can deduct (with the stock now) or what it took (when, who).</summary>
public sealed class ExamReportStockDto
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int? InStock { get; set; }
    public DateTime? AtUtc { get; set; }
    public string? By { get; set; }
}

public sealed class ExamReportSectionsDto
{
    public bool PatientData { get; set; }
    public bool Method { get; set; }
    public bool Interpretation { get; set; }
    public bool ReportedBy { get; set; }
    public bool DirectionArrows { get; set; } = true;
    public bool PreviousResult { get; set; }
    public bool LotNumber { get; set; }
    public bool CriteriaTable { get; set; }
    public bool InterpretationAtFoot { get; set; }
}

/// <summary>The criteria table printed under the results; each row has one text per column.</summary>
public sealed class CriteriaTableDto
{
    public string? Title { get; set; }
    /// <summary>Which cells print as headings; the first row's texts are <see cref="Columns"/>.</summary>
    public CriteriaHeaders Headers { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
}

public sealed class ExamReportResultDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReportParameterKind Kind { get; set; }
    public string? Unit { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Band { get; set; }
    public bool Abnormal { get; set; }
    public ResultDirection Direction { get; set; }
    public decimal? BarMin { get; set; }
    public decimal? BarMax { get; set; }
    public string? PreviousValue { get; set; }
}

/// <summary>The clinic's exam report settings (branding and templates) with the version they were read at.</summary>
public sealed class ExamReportSettingsDocument
{
    public int Version { get; set; }
    public JsonElement Settings { get; set; }
}

/// <summary>A logo or background image; at most 3 MB, PNG, JPEG or WebP.</summary>
public sealed class ClinicImageRequest
{
    [Required, RegularExpression("^image/(png|jpeg|webp)$", ErrorMessage = "Use a PNG, JPEG or WebP image.")]
    public string ContentType { get; init; } = string.Empty;

    [Required, MaxLength(3 * 1024 * 1024, ErrorMessage = "The image must be 3 MB or smaller.")]
    public byte[] Data { get; init; } = [];
}
