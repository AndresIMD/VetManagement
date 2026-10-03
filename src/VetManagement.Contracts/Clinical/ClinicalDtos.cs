using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using VetManagement.Contracts.Medical;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Clinical;

/// <summary>With a protocol, the name, kind and next due date come from the clinic's settings (each can be overridden).</summary>
public sealed class RecordDoseRequest
{
    [MaxLength(100)]
    public string? ProtocolCode { get; init; }

    [MaxLength(200)]
    public string? ProductName { get; init; }

    public PreventiveKind? Kind { get; init; }

    public DateOnly AppliedOn { get; init; }

    /// <summary>Overrides the protocol's interval; leave empty to use it.</summary>
    public DateOnly? NextDueOn { get; init; }

    [MaxLength(100)]
    public string? BatchNumber { get; init; }

    public int? VisitId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    /// <summary>Inventory product applied; one unit leaves stock when the clinic enables it.</summary>
    public int? ItemId { get; init; }
}

/// <summary>A drug or material from inventory used during a visit.</summary>
public sealed class AddVisitSupplyRequest
{
    public int ItemId { get; init; }

    [Range(1, 10_000)]
    public int Quantity { get; init; } = 1;

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed class VisitSupplyDto
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public bool StockDeducted { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class PreventiveDoseDto
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public string? ProtocolCode { get; set; }
    public PreventiveKind Kind { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateOnly AppliedOn { get; set; }
    public DateOnly? NextDueOn { get; set; }
    public PreventiveStatus Status { get; set; }
    public string? AppliedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReminderSentAtUtc { get; set; }
    public int? ItemId { get; set; }
}

public sealed class PetHistoryDto
{
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public Species Species { get; set; }
    public string Breed { get; set; } = string.Empty;
    public Sex Sex { get; set; }
    public DateTime Birthdate { get; set; }
    public float Weight { get; set; }
    public int OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
    public List<MedicalVisitDto> Visits { get; set; } = [];
    public List<PreventiveDoseDto> Doses { get; set; } = [];
    /// <summary>Supplies used in the visits above (match them by VisitId).</summary>
    public List<VisitSupplyDto> Supplies { get; set; } = [];
}

/// <summary>A pet whose next preventive dose is due, with the owner's contact for a call or message.</summary>
public sealed class DueDoseDto
{
    public PreventiveDoseDto Dose { get; set; } = new();
    public string PetName { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
}

public sealed class ClinicalSettingsDocument
{
    public int Version { get; set; }
    public JsonElement Settings { get; set; }
}
