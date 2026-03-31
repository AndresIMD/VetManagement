using System.ComponentModel.DataAnnotations;
using VetManagement.Shared.Enums;

namespace VetManagement.Shared.Models.Exams;

/// <summary>
/// Represents a single exam within an ExamPerformed request/order.
/// Holds per-exam state, externalization info and case-specific attributes.
/// </summary>
public class ExamRequestItem
{
    public int Id { get; set; }

    [Required]
    public int ExamId { get; set; }

    public ExamItemStatus Status { get; set; } = ExamItemStatus.Pending;

    public bool IsExternal { get; set; }

    public int? ExternalEstimatedDays { get; set; }

    public int? ExternalLabId { get; set; }

    public ExternalLab? ExternalLab { get; set; }

    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
}
