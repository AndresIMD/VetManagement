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
    [Range(1, int.MaxValue)]
    public int PetId { get; init; }

    [Range(1, int.MaxValue)]
    public int ExamId { get; init; }

    public DateOnly ReceivedOn { get; init; }

    [MaxLength(200)]
    public string? ReferringVet { get; init; }

    public Dictionary<string, string> Values { get; init; } = [];

    /// <summary>Written interpretation; leave empty to use the one the bands give.</summary>
    [MaxLength(2000)]
    public string? Interpretation { get; init; }
}

public sealed class ExamReportDto
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public int ExamId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Method { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public string? ReferringVet { get; set; }
    public string? Interpretation { get; set; }
    public ExamReportSectionsDto Sections { get; set; } = new();
    public List<ExamReportResultDto> Results { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    public string PetName { get; set; } = string.Empty;
    public Species Species { get; set; }
    public string? Breed { get; set; }
    public Sex Sex { get; set; }
    public DateTime? Birthdate { get; set; }
    public string? OwnerName { get; set; }
}

public sealed class ExamReportSectionsDto
{
    public bool PatientData { get; set; }
    public bool Method { get; set; }
    public bool Interpretation { get; set; }
    public bool ReportedBy { get; set; }
}

public sealed class ExamReportResultDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Band { get; set; }
    public bool Abnormal { get; set; }
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
