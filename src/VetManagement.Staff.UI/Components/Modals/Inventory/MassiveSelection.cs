namespace VetManagement.Staff.UI.Components.Modals.Inventory;

/// <summary>What a bulk dialog returns: the items and quantities, and the warehouse (or, for a transfer, the two).</summary>
public sealed record MassiveSelection<TItem>(List<TItem> Items, int? WarehouseId, int? ToWarehouseId);
