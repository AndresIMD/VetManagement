using MudBlazor;

namespace VetManagement.Shared.Helpers;

public static class InventoryUiHelpers
{
    /// <summary>
    /// Returns a CSS class for stock value highlighting based on low/critical stock levels.
    /// </summary>
    /// <param name="stock">Current stock quantity.</param>
    /// <param name="lowStockThreshold">Threshold for low stock alert.</param>
    /// <returns>CSS class string for styling.</returns>
    public static string GetStockCss(int stock, int lowStockThreshold)
    {
        var effective = lowStockThreshold > 0 ? lowStockThreshold : 5;
        return stock <= effective ? "text-danger fw-bold" : string.Empty;
    }

    /// <summary>
    /// Returns a human-friendly label for an inventory alert code.
    /// </summary>
    /// <param name="alert">Alert code (e.g., "low", "zero").</param>
    /// <returns>Localized alert label.</returns>
    public static string GetAlertLabel(string alert) => alert switch
    {
        "low" => "Alert: Low Stock",
        "zero" => "Alert: Out of Stock",
        _ => $"Alert: {alert}"
    };

    /// <summary>
    /// Chooses an appropriate MudBlazor color for an alert type.
    /// </summary>
    /// <param name="alert">Alert code.</param>
    /// <returns>MudBlazor Color enum value.</returns>
    public static Color GetAlertColor(string alert) => alert switch
    {
        "low" => Color.Warning,
        "zero" => Color.Error,
        _ => Color.Tertiary
    };
}
