using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Medical;

/// <summary>
/// A patient's visit to the clinic with its billed procedures. Domain entity — no validation or UI attributes.
/// </summary>
public class MedicalVisit : Entity<int>
{
    public MedicalVisit() : base(0) { }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public int PatientId { get; set; }

    public string? RecordNumber { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string Responsible { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? BudgetNumber { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public int TotalValue { get; set; }

    /// <summary>Appointment this visit attends, when it came from the agenda.</summary>
    public int? AppointmentId { get; set; }

    // Clinical record (ficha clínica)
    public string? Reason { get; set; }

    public string? Anamnesis { get; set; }

    public string? Examination { get; set; }

    public string? Diagnosis { get; set; }

    public string? Treatment { get; set; }

    public decimal? WeightKg { get; set; }

    public decimal? TemperatureC { get; set; }

    public List<VisitProcedure> Procedures { get; set; } = [];
}

/// <summary>
/// A procedure performed during a <see cref="MedicalVisit"/>, optionally linked to a catalog exam.
/// </summary>
public class VisitProcedure : Entity<int>
{
    public VisitProcedure() : base(0) { }

    public int? ExamId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Price { get; set; }

    public string? Notes { get; set; }
}
