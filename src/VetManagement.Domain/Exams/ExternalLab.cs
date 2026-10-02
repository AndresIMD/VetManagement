using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// Third-party laboratory that processes exams the clinic does not run in-house.
/// </summary>
public class ExternalLab : Entity<int>
{
    public ExternalLab() : base(0) { }

    public string Name { get; set; } = string.Empty;

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public int? DefaultTurnaroundDays { get; set; }
}
