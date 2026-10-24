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

    /// <summary>The registered pet; null when the patient was typed into the report (<see cref="Patient"/>).</summary>
    public int? PetId { get; set; }

    /// <summary>Patient and owner of a report for an animal that isn't registered (e.g. referred by another clinic).</summary>
    public ReportPatient? Patient { get; set; }

    public int ExamId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Brand and machine of the exam, e.g. "Vcheck - BIONOTE".</summary>
    public string? Method { get; set; }

    public DateOnly ReceivedOn { get; set; }

    /// <summary>Vet who requested the exam ("médico tratante"), often from another clinic: free text.</summary>
    public string? ReferringVet { get; set; }

    /// <summary>Lot of the test kit or reagent, for exams that require it; optional.</summary>
    public string? LotNumber { get; set; }

    public string? Interpretation { get; set; }

    public ExamReportSections Sections { get; set; } = new();

    public List<ExamReportResult> Results { get; set; } = [];

    /// <summary>Copy of the template's criteria table when it prints; null otherwise.</summary>
    public CriteriaTable? Criteria { get; set; }

    /// <summary>Reception date of the previous report copied into <see cref="ExamReportResult.PreviousValue"/>; null = none.</summary>
    public DateOnly? PreviousReceivedOn { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// The inventory product this report took ("deduct from inventory"), with its name and quantity at the time, so returning
    /// it puts back exactly that whatever the catalog says later. Null when nothing was taken, or it was returned.
    /// </summary>
    public int? StockItemId { get; set; }
    public string? StockItemName { get; set; }
    public int StockQuantity { get; set; }
    public DateTime? StockDeductedAtUtc { get; set; }
    public string? StockDeductedBy { get; set; }

    public bool StockDeducted => StockItemId is not null;

    /// <summary>The band labels: just the label for a one-parameter exam, "Name: label" lines otherwise.</summary>
    public static string? InterpretationFrom(IReadOnlyList<ExamReportResult> results)
    {
        var labelled = results.Where(r => !string.IsNullOrWhiteSpace(r.Band)).ToList();
        if (labelled.Count == 0)
            return null;
        return results.Count(r => r.Kind.TakesValue()) == 1
            ? labelled[0].Band
            : string.Join("\n", labelled.Select(r => $"{r.Name}: {r.Band}"));
    }
}

/// <summary>The patient of a report typed by hand, kept only in the report.</summary>
public class ReportPatient
{
    public string Name { get; set; } = string.Empty;
    public Species Species { get; set; }
    public string? Breed { get; set; }
    public Sex Sex { get; set; }
    public DateOnly? Birthdate { get; set; }
    /// <summary>The birth date was worked out from an approximate age ("5 years 6 months"), as owners usually give it.</summary>
    public bool BirthdateApproximate { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerLastName { get; set; }
}

/// <summary>One printed line: the parameter as it was configured when the report was saved, and its value.</summary>
public class ExamReportResult
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Text results (Positivo / Negativo, blood types) print as highlighted results when a report has only those; a
    /// section line prints its name as a title over the results that follow it; a page break line starts a new page.
    /// </summary>
    public ReportParameterKind Kind { get; set; }
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
