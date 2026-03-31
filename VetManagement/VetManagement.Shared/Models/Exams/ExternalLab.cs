using System.ComponentModel.DataAnnotations;

namespace VetManagement.Shared.Models.Exams;

public class ExternalLab
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int? DefaultTurnaroundDays { get; set; }
}
