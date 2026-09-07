namespace VetManagement.Shared.Enums;

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
