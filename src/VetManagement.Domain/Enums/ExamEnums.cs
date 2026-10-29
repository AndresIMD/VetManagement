namespace VetManagement.Domain.Enums;

/// <summary>
/// Type of biological sample required for an exam.
/// </summary>
public enum SampleType
{
    [DisplayString("None")]
    None = 0,
    [DisplayString("Blood")]
    Blood,
    [DisplayString("Urine")]
    Urine,
    [DisplayString("Feces")]
    Feces,
    [DisplayString("Tissue")]
    Tissue,
    [DisplayString("Swab")]
    Swab,
    [DisplayString("Other")]
    Other,
    // Added later: the stored numbers of the values above must not change. Forms list them in DisplayOrder.
    [DisplayString("Plasma")]
    Plasma = 7,
    [DisplayString("Serum")]
    Serum = 8,
    [DisplayString("Supernatant")]
    Supernatant = 9
}

/// <summary>
/// Type of container used to hold a biological sample.
/// </summary>
public enum SampleContainer
{
    [DisplayString("None")]
    None = 0,
    [DisplayString("Tube")]
    Tube,
    [DisplayString("Swab")]
    Swab,
    [DisplayString("Slide")]
    Slide,
    [DisplayString("Jar")]
    Jar,
    [DisplayString("Pot")]
    Pot,
    [DisplayString("Other")]
    Other,
    // Added later: the stored numbers of the values above must not change. Forms list them in DisplayOrder.
    /// <summary>Red top: no additive (serum).</summary>
    [DisplayString("Red tube")]
    RedTube = 7,
    /// <summary>Light blue top: sodium citrate (coagulation).</summary>
    [DisplayString("Light blue tube")]
    LightBlueTube = 8,
    /// <summary>Green top: heparin.</summary>
    [DisplayString("Green tube")]
    GreenTube = 9,
    /// <summary>Yellow top: separator gel (serum).</summary>
    [DisplayString("Yellow tube")]
    YellowTube = 10,
    /// <summary>Lavender top: EDTA (haematology).</summary>
    [DisplayString("Lavender tube")]
    LavenderTube = 11
}

/// <summary>The order the exam form lists sample types and containers in (related values together, "Other" last).</summary>
public static class ExamSampleOrder
{
    public static readonly SampleType[] SampleTypes =
    [
        SampleType.Blood, SampleType.Plasma, SampleType.Serum, SampleType.Urine, SampleType.Supernatant,
        SampleType.Feces, SampleType.Tissue, SampleType.Swab, SampleType.Other
    ];

    public static readonly SampleContainer[] Containers =
    [
        SampleContainer.Tube, SampleContainer.RedTube, SampleContainer.LightBlueTube, SampleContainer.GreenTube,
        SampleContainer.YellowTube, SampleContainer.LavenderTube, SampleContainer.Swab, SampleContainer.Slide,
        SampleContainer.Jar, SampleContainer.Pot, SampleContainer.Other
    ];
}

/// <summary>
/// Status of an individual exam item within a request.
/// </summary>
public enum ExamItemStatus
{
    [DisplayString("None")]
    None,
    [DisplayString("Pending")]
    Pending,
    [DisplayString("In Progress")]
    InProgress,
    [DisplayString("Completed")]
    Completed,
    [DisplayString("Cancelled")]
    Cancelled
}

/// <summary>Paper the exam reports are printed on.</summary>
public enum PaperSize
{
    [DisplayString("Letter")]
    Letter,
    [DisplayString("A4")]
    A4
}

/// <summary>
/// What a report parameter holds: a measured number (evaluated against bands) or free text; or, taking no value, a
/// section title that groups the parameters under it (e.g. "VALORES DE OXIMETRÍA") or a page break, which prints
/// nothing and starts a new page. Issued reports store the number, so the values never change.
/// </summary>
public enum ReportParameterKind
{
    [DisplayString("Number")]
    Number,
    [DisplayString("Text")]
    Text,
    [DisplayString("Section title")]
    Section = 2,
    [DisplayString("Page break")]
    PageBreak = 3
}

public static class ReportParameterKindExtensions
{
    /// <summary>Numbers and texts hold a result; section titles and page breaks only shape the printed report.</summary>
    public static bool TakesValue(this ReportParameterKind kind) => kind is ReportParameterKind.Number or ReportParameterKind.Text;
}

/// <summary>Images the clinic uploads for its printed documents.</summary>
public enum ClinicImageKind
{
    [DisplayString("Report logo")]
    ReportLogo = 1,
    [DisplayString("Report background")]
    ReportBackground = 2
}

/// <summary>What the reference column of a report parameter shows.</summary>
public enum ReferenceDisplay
{
    [DisplayString("Normal range")]
    Range,
    [DisplayString("Written text")]
    Text,
    [DisplayString("Nothing")]
    None
}

/// <summary>
/// Which cells of a report's criteria table print as headings: the first row (the default), the first column, both (a
/// two-way table, e.g. ranges by age across the top and by weight down the side) or none.
/// </summary>
public enum CriteriaHeaders
{
    [DisplayString("First row")]
    FirstRow,
    [DisplayString("First column")]
    FirstColumn,
    [DisplayString("First row and column")]
    Both,
    [DisplayString("None")]
    None
}

public static class CriteriaHeadersExtensions
{
    public static bool InFirstRow(this CriteriaHeaders headers) => headers is CriteriaHeaders.FirstRow or CriteriaHeaders.Both;

    public static bool InFirstColumn(this CriteriaHeaders headers) => headers is CriteriaHeaders.FirstColumn or CriteriaHeaders.Both;
}

/// <summary>Where a report's background image is drawn.</summary>
public enum BackgroundArea
{
    [DisplayString("Whole page")]
    Page,
    [DisplayString("Results box")]
    ResultsBox
}

/// <summary>Which side of an image keeping its proportions fills its area.</summary>
public enum ImageFit
{
    [DisplayString("Width")]
    Width,
    [DisplayString("Height")]
    Height
}

/// <summary>How the texts of a report's result columns sit; the parameter names always align left.</summary>
public enum ColumnAlign
{
    [DisplayString("Centered")]
    Center,
    [DisplayString("Left")]
    Left
}

/// <summary>Where an image that doesn't fill its whole area sits: top/left, centre or bottom/right.</summary>
public enum ImageAlign
{
    [DisplayString("Start")]
    Start,
    [DisplayString("Center")]
    Center,
    [DisplayString("End")]
    End
}

/// <summary>Comparison of an interpretation rule's condition.</summary>
public enum RuleOperator
{
    [DisplayString("<")]
    LessThan,
    [DisplayString("≤")]
    LessOrEqual,
    [DisplayString(">")]
    GreaterThan,
    [DisplayString("≥")]
    GreaterOrEqual
}

/// <summary>Where a result falls against its normal range.</summary>
public enum ResultDirection
{
    [DisplayString("Within range")]
    None,
    [DisplayString("Low")]
    Low,
    [DisplayString("High")]
    High
}
