using System.ComponentModel.DataAnnotations;

namespace VetManagement.Contracts.Inventory;

/// <summary>A warehouse: the daily-use one (everyone sees it, the automatic stock changes use it) or one seen by its roles.</summary>
public sealed class WarehouseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDailyUse { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>Roles that see the warehouse; sent only to whoever manages warehouses.</summary>
    public List<string> Roles { get; set; } = [];
}

/// <summary>Creates or changes a warehouse. The daily-use one takes only the name.</summary>
public sealed class WarehouseRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    public List<string> Roles { get; init; } = [];

    public bool IsArchived { get; init; }
}

/// <summary>Units of an item in one warehouse.</summary>
public sealed class WarehouseUnitsDto
{
    public int WarehouseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

/// <summary>Moves units of an item between two warehouses (the daily-use one when an id is missing).</summary>
public sealed class TransferStockRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    public int? FromWarehouseId { get; init; }

    public int? ToWarehouseId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    [MaxLength(200)]
    public string? Reason { get; init; }
}

/// <summary>Moves several items between two warehouses; each moves at most what the first one holds.</summary>
public sealed class MassTransferRequest
{
    public int? FromWarehouseId { get; init; }

    public int? ToWarehouseId { get; init; }

    [MaxLength(200)]
    public string? Reason { get; init; }

    [Required, MinLength(1)]
    public List<MassStockUpdateRequest> Items { get; init; } = [];
}
