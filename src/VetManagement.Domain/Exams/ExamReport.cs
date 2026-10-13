using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// An issued exam report. Title, sections and result lines (names, units, references) are copied from the
/// template when saved, so later template changes never alter a report already given to an owner.
/// Patient and owner details are read live from the pet when printed.
/// </summary>
public class ExamReport : Entity<int>
{
    public ExamReport() : base(0) { }

    public int PetId { get; set; }

    public int ExamId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Brand and machine of the exam, e.g. "Vcheck - BIONOTE".</summary>
    public string? Method { get; set; }

    public DateOnly ReceivedOn { get; set; }

    /// <summary>Vet who requested the exam ("médico tratante"), often from another clinic: free text.</summary>
    public string? ReferringVet { get; set; }

    public string? Interpretation { get; set; }

    public ExamReportSections Sections { get; set; } = new();

    public List<ExamReportResult> Results { get; set; } = [];

    /// <summary>Reception date of the previous report copied into <see cref="ExamReportResult.PreviousValue"/>; null = none.</summary>
    public DateOnly? PreviousReceivedOn { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>The band labels: just the label for a one-parameter exam, "Name: label" lines otherwise.</summary>
    public static string? InterpretationFrom(IReadOnlyList<ExamReportResult> results)
    {
        var labelled = results.Where(r => !string.IsNullOrWhiteSpace(r.Band)).ToList();
        if (labelled.Count == 0)
            return null;
        return results.Count == 1
            ? labelled[0].Band
            : string.Join("\n", labelled.Select(r => $"{r.Name}: {r.Band}"));
    }
}

/// <summary>One printed line: the parameter as it was configured when the report was saved, and its value.</summary>
public class ExamReportResult
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Reference { get; set; }
    /// <summary>Label of the band the value fell in, if any.</summary>
    public string? Band { get; set; }
    public bool Abnormal { get; set; }
    public ResultDirection Direction { get; set; }
    /// <summary>Normal range drawn as a bar; set only when the parameter shows one and the range has both limits.</summary>
    public decimal? BarMin { get; set; }
    public decimal? BarMax { get; set; }
    /// <summary>The same parameter's value in the previous report (when the template shows that column).</summary>
    public string? PreviousValue { get; set; }
}
