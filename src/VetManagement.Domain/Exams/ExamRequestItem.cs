using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// One exam inside an <see cref="ExamPerformed"/> order, processed in-house or by an <see cref="ExternalLab"/>.
/// </summary>
public class ExamRequestItem : Entity<int>
{
    public ExamRequestItem() : base(0) { }

    public int ExamId { get; set; }

    public ExamItemStatus Status { get; set; } = ExamItemStatus.Pending;

    public bool IsExternal { get; set; }

    public int? ExternalEstimatedDays { get; set; }

    public int? ExternalLabId { get; set; }

    public ExternalLab? ExternalLab { get; set; }

    public Dictionary<string, string> Attributes { get; set; } = [];
}
