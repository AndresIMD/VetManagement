using VetManagement.Contracts.Inventory;

namespace VetManagement.Contracts.Exams;

/// <summary>Reports received in one slice of the period.</summary>
public sealed record ExamMetricsBucketDto(DateTime Start, int Reports);

/// <summary>Reports of one exam in the period, and how many of them have a value out of its range.</summary>
public sealed record ExamCountDto(int ExamId, string Name, int Reports, int WithAbnormal);

/// <summary>Reports written by one user in the period.</summary>
public sealed record ReporterCountDto(string User, int Reports);

/// <summary>A product taken from inventory by the reports of the period: in how many reports, and how many units.</summary>
public sealed record KitUseDto(string Name, int Reports, int Units);

/// <summary>
/// Lab reports between two dates (inclusive), by the date the sample was received, as the report list shows them.
/// "Previous" is the period of the same length right before. <see cref="WithAbnormal"/> counts the reports with at
/// least one value out of its range, out of all the reports of the period.
/// </summary>
public sealed record ExamMetricsDto(
    DateOnly From,
    DateOnly To,
    MetricsGrouping Grouping,
    int Reports,
    int PreviousReports,
    int WithAbnormal,
    int KitUnits,
    List<ExamMetricsBucketDto> Buckets,
    List<ExamCountDto> ByExam,
    List<ReporterCountDto> ByReporter,
    List<KitUseDto> Kits);
