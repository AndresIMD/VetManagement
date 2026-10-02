using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Medical;

/// <summary>Body for creating or updating a medical visit with its procedures.</summary>
public sealed class MedicalVisitRequest
{
    public DateTime Date { get; init; } = DateTime.UtcNow;

    public int PatientId { get; init; }

    public string? RecordNumber { get; init; }

    public string PatientName { get; init; } = string.Empty;

    public string Responsible { get; init; } = string.Empty;

    public string? Location { get; init; }

    public string? BudgetNumber { get; init; }

    public PaymentStatus PaymentStatus { get; init; } = PaymentStatus.Pending;

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Cash;

    [Range(0, int.MaxValue)]
    public int TotalValue { get; init; }

    public List<VisitProcedureRequest> Procedures { get; init; } = [];
}

/// <summary>A procedure of a visit. <see cref="Id"/> identifies an existing procedure when updating (0 = new).</summary>
public sealed class VisitProcedureRequest
{
    public int Id { get; init; }

    public int? ExamId { get; init; }

    [Required]
    public string Name { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Price { get; init; }

    public string? Notes { get; init; }
}

public sealed class MedicalVisitDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int PatientId { get; set; }
    public string? RecordNumber { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? BudgetNumber { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int TotalValue { get; set; }
    public List<VisitProcedureDto> Procedures { get; set; } = [];
}

public sealed class VisitProcedureDto
{
    public int Id { get; set; }
    public int? ExamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public string? Notes { get; set; }
}
