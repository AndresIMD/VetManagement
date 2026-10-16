namespace VetManagement.Staff.UI.Resources;

/// <summary>
/// Validation messages of the UI models, translated when shown:
/// <c>[Required(ErrorMessageResourceType = typeof(ValidationTexts), ErrorMessageResourceName = nameof(ValidationTexts.Required))]</c>.
/// </summary>
public static class ValidationTexts
{
    public static string Required => Strings.T("This field is required.");
    public static string InvalidEmail => Strings.T("Invalid email format.");
    public static string MinDoseNotNegative => Strings.T("The minimum dose must be 0 or greater.");
    public static string AmountNotNegative => Strings.T("The amount (mL) must be 0 or greater.");
    public static string ConcentrationRange => Strings.T("Concentration must be between 0 and 100.");
    public static string SellPriceNotNegative => Strings.T("The selling price must be 0 or greater.");
    public static string BuyPriceNotNegative => Strings.T("The purchase price must be 0 or greater.");
    public static string LowStockThresholdNotNegative => Strings.T("The low stock threshold must be 0 or greater.");
    public static string PriceNotNegative => Strings.T("The price cannot be less than 0.");
}
