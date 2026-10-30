using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Models.DTOs;

public class InventoryItemDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Brand { get; set; } = "";
    public string? Compound { get; set; } = "";
    public ItemType Type { get; set; } = ItemType.None;
    /// <summary>Units in the warehouses the user sees.</summary>
    public int Stock { get; set; }
    /// <summary>Units in the daily-use warehouse, the ones visits, sales and doses take.</summary>
    public int DailyUseStock { get; set; }
    public List<WarehouseUnitsDto> Warehouses { get; set; } = [];
    /// <summary>Other warehouses holding some, by name: where to ask for more.</summary>
    public List<string> AskAt { get; set; } = [];
    public int SellPrice { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? BrandBarcode { get; set; } = string.Empty;
    public int LowStockThreshold { get; set; } = 5;
}

public class InventoryMassUpdateDTO
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

