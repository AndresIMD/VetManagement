using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Consumption;

/// <summary>A sheet in the list: who, since when and what it adds up to so far.</summary>
public sealed record ConsumptionSheetSummaryDto(int Id, int? PetId, string PatientName, string? OwnerName, DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc, int EntryCount, int Total);

public sealed record ConsumptionEntryDto(int Id, int ItemId, string ItemName, ConsumptionCharge Charge, decimal Quantity, bool Discarded,
    string? Notes, DateTime UsedAtUtc, string? CreatedBy);

/// <summary>What one item adds up to on the sheet (see ConsumptionSummary).</summary>
public sealed record ConsumptionLineDto(int ItemId, string ItemName, ConsumptionCharge Charge, decimal Given, decimal Discarded,
    int Containers, decimal LeftMl, decimal? ContainerMl, int UnitPrice, int Total);

public sealed record ConsumptionSheetDto(int Id, int? PetId, string PatientName, string? OwnerName, string? Notes,
    DateTime OpenedAtUtc, string? OpenedBy, DateTime? ClosedAtUtc, string? ClosedBy,
    List<ConsumptionEntryDto> Entries, List<ConsumptionLineDto> Lines, int Total);

/// <summary>A registered pet (<see cref="PetId"/>) or a patient typed in.</summary>
public sealed class OpenConsumptionSheetRequest
{
    [Range(1, int.MaxValue)]
    public int? PetId { get; init; }

    [MaxLength(100)]
    public string? PatientName { get; init; }

    [MaxLength(200)]
    public string? OwnerName { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}

public sealed class AddConsumptionEntryRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    /// <summary>Units, or mL for items charged by mL or by container.</summary>
    public decimal Quantity { get; init; }

    [MaxLength(200)]
    public string? Notes { get; init; }
}

public sealed record ConsumptionPricingDto(int ItemId, string ItemName, int SellPrice, ConsumptionCharge Charge, decimal? ContainerMl,
    int? PricePerMl, bool Controlled);

public sealed class ConsumptionPricingRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    public ConsumptionCharge Charge { get; init; }

    public decimal? ContainerMl { get; init; }

    [Range(0, int.MaxValue)]
    public int? PricePerMl { get; init; }

    public bool Controlled { get; init; }
}
