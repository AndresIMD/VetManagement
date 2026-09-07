namespace VetManagement.Domain.Enums;

/// <summary>
/// General category of an inventory item.
/// </summary>
public enum ItemType
{
    [DisplayString("None")]
    None,
    [DisplayString("Material")]
    Material,
    [DisplayString("Drug")]
    Drug,
    [DisplayString("Tool")]
    Tool
}

/// <summary>
/// Type of inventory movement for stock tracking.
/// </summary>
public enum InventoryMovementType
{
    [DisplayString("None")]
    None,
    [DisplayString("Ingress")]
    Ingress,
    [DisplayString("Egress")]
    Egress,
    [DisplayString("Adjustment")]
    Adjustment,
    [DisplayString("Massive Stock Ingress")]
    MassiveStockIngress,
    [DisplayString("Massive Stock Egress")]
    MassiveStockEgress
}

/// <summary>
/// Represents the stock status of an item relative to its low-stock threshold.
/// </summary>
public enum StockStatus
{
    [DisplayString("None")]
    None,
    [DisplayString("In Stock")]
    InStock,
    [DisplayString("Low Stock")]
    LowStock,
    [DisplayString("Out of Stock")]
    OutOfStock,
    [DisplayString("Reserved")]
    Reserved
}

/// <summary>
/// Filter criteria for stock alert levels in item queries.
/// </summary>
public enum StockAlertFilter
{
    [DisplayString("None")]
    None = 0,
    [DisplayString("Low Stock")]
    LowStock = 1,
    [DisplayString("Out of Stock")]
    OutOfStock = 2
}

/// <summary>
/// Field to sort items by in paged queries.
/// </summary>
public enum ItemSortField
{
    Id,
    Name,
    Type,
    Brand,
    Stock,
    SellPrice,
    BuyPrice,
    LowStockThreshold,
    Barcode,
    BrandBarcode
}

/// <summary>
/// Sort direction for queries.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}