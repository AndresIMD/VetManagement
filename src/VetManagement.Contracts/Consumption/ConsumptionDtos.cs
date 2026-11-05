using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Consumption;

/// <summary>A sheet in the list: who, since when and what it adds up to so far.</summary>
/// <param name="StockPending">Units its uses need that the daily-use warehouse didn't have when they were recorded.</param>
/// <param name="Pending">The same units, item by item.</param>
/// <param name="UsedUp">The budgeted items with nothing left: a new budget is needed.</param>
public sealed record ConsumptionSheetSummaryDto(int Id, int? PetId, string PatientName, string? OwnerName, DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc, int EntryCount, int Total, int StockPending, List<ConsumptionPendingDto> Pending, List<ConsumptionBalanceDto> UsedUp);

/// <summary>Units of one item a sheet still has to take from inventory.</summary>
public sealed record ConsumptionPendingDto(string ItemName, int Units);

public sealed record ConsumptionEntryDto(int Id, int ItemId, string ItemName, ConsumptionCharge Charge, decimal Quantity, bool Discarded,
    string? Notes, DateTime UsedAtUtc, string? CreatedBy);

/// <summary>What one item adds up to on the sheet (see ConsumptionSummary).</summary>
/// <param name="StockPending">Units of it not taken from inventory yet: the daily-use warehouse had too few.</param>
public sealed record ConsumptionLineDto(int ItemId, string ItemName, ConsumptionCharge Charge, decimal Given, decimal Discarded,
    int Containers, decimal LeftMl, decimal? ContainerMl, int UnitPrice, int Total, int StockPending);

/// <param name="MovesStock">The sheet takes its uses from inventory; false on sheets opened before sheets did.</param>
/// <param name="Budgets">What the guardian approved, newest first.</param>
/// <param name="Balances">What is left of each budgeted item.</param>
public sealed record ConsumptionSheetDto(int Id, int? PetId, string PatientName, string? OwnerName, string? Notes,
    DateTime OpenedAtUtc, string? OpenedBy, DateTime? ClosedAtUtc, string? ClosedBy,
    List<ConsumptionEntryDto> Entries, List<ConsumptionLineDto> Lines, int Total, bool MovesStock,
    List<ConsumptionBudgetDto> Budgets, List<ConsumptionBalanceDto> Balances);

/// <summary>One approval: a quantity of an item, in its charge unit (units, containers or mL).</summary>
public sealed record ConsumptionBudgetDto(int Id, int ItemId, string ItemName, ConsumptionCharge Charge, decimal Quantity, string? Notes,
    DateTime ApprovedAtUtc, string? CreatedBy);

/// <summary>What is left of one item's budget, in its unit (see ConsumptionBalance).</summary>
/// <param name="LeftMl">mL still in the open container.</param>
/// <param name="UsedUp">Nothing left to give: a new budget is needed.</param>
/// <param name="ToRefund">Never started, to give back at discharge.</param>
public sealed record ConsumptionBalanceDto(int ItemId, string ItemName, ConsumptionCharge Charge, decimal Budgeted, decimal Used, decimal Left,
    decimal LeftMl, bool UsedUp, decimal ToRefund);

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

/// <summary>What the guardian approved of an item: units, containers or mL, as the item is charged.</summary>
public sealed class AddConsumptionBudgetRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    public decimal Quantity { get; init; }

    [MaxLength(200)]
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

/// <param name="TakenOutBeforehand">It leaves inventory when a quick-use box is restocked: sheets record its uses without taking stock.</param>
public sealed record ConsumptionPricingDto(int ItemId, string ItemName, int SellPrice, ConsumptionCharge Charge, decimal? ContainerMl,
    int? PricePerMl, bool Controlled, bool TakenOutBeforehand);

public sealed class ConsumptionPricingRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    public ConsumptionCharge Charge { get; init; }

    public decimal? ContainerMl { get; init; }

    [Range(0, int.MaxValue)]
    public int? PricePerMl { get; init; }

    public bool Controlled { get; init; }

    /// <summary>It leaves inventory when a quick-use box is restocked: sheets record its uses without taking stock.</summary>
    public bool TakenOutBeforehand { get; init; }
}
