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
    Other
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
    Other
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

/// <summary>What a report parameter holds: a measured number (evaluated against bands) or free text.</summary>
public enum ReportParameterKind
{
    [DisplayString("Number")]
    Number,
    [DisplayString("Text")]
    Text
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
