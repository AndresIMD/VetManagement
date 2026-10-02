using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Scheduling;

public sealed record AvailableSlotDto(string ResourceCode, DateTime StartUtc, DateTime EndUtc, bool IsOverflow);

/// <summary>Staff booking. <see cref="Overbook"/> books beyond capacity (sobrecupo) on purpose.</summary>
public sealed class CreateAppointmentRequest
{
    [Required, MaxLength(100)]
    public string ServiceCode { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string ResourceCode { get; init; } = string.Empty;

    public DateTime StartUtc { get; init; }

    public bool Overbook { get; init; }

    [Required, MaxLength(200)]
    public string OwnerName { get; init; } = string.Empty;

    public int? ClientId { get; init; }
    public int? PetId { get; init; }

    [MaxLength(20)]
    public string? OwnerTaxId { get; init; }

    [MaxLength(256), EmailAddress]
    public string? OwnerEmail { get; init; }

    [MaxLength(30)]
    public string? OwnerPhone { get; init; }

    [MaxLength(100)]
    public string? PetName { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class RescheduleAppointmentRequest
{
    [Required, MaxLength(100)]
    public string ResourceCode { get; init; } = string.Empty;

    public DateTime StartUtc { get; init; }

    public bool Overbook { get; init; }
}

public sealed class CancelAppointmentRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}

public sealed class AppointmentDto
{
    public int Id { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public AppointmentStatus Status { get; set; }
    public AppointmentSource Source { get; set; }
    public bool IsOverbooked { get; set; }
    public int? ClientId { get; set; }
    public int? PetId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerTaxId { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
    public string? PetName { get; set; }
    public string? Notes { get; set; }
    public int Price { get; set; }
    public int DepositAmount { get; set; }
    public string? CancelReason { get; set; }
}
