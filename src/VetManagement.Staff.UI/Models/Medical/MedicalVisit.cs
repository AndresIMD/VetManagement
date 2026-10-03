using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Models.Medical;

/// <summary>
/// Medical visit linked to a patient and procedures performed.
/// </summary>
public class MedicalVisit
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Required]
    public int PatientId { get; set; }

    public string? RecordNumber { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string Responsible { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? BudgetNumber { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Range(0, int.MaxValue)]
    public int TotalValue { get; set; }

    public int? AppointmentId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(4000)]
    public string? Anamnesis { get; set; }

    [MaxLength(4000)]
    public string? Examination { get; set; }

    [MaxLength(2000)]
    public string? Diagnosis { get; set; }

    [MaxLength(4000)]
    public string? Treatment { get; set; }

    [Range(0.01, 500)]
    public decimal? WeightKg { get; set; }

    [Range(25, 45)]
    public decimal? TemperatureC { get; set; }

    public List<VisitProcedure> Procedures { get; set; } = new List<VisitProcedure>();
}

/// <summary>
/// Procedure or service applied during a medical visit.
/// </summary>
public class VisitProcedure
{
    public int Id { get; set; }

    public int? ExamId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Price { get; set; }

    public string? Notes { get; set; }
}
