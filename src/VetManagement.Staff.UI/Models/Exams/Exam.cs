using System.ComponentModel.DataAnnotations;
using VetManagement.Staff.UI.Constants;
using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Models.Exams;

public class Exam
{
    public int Id { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string Machine { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public SampleType SampleType { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public SampleContainer SampleContainer { get; set; }

    [Range(0, int.MaxValue, ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.PriceNotNegative))]
    public int BuyPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.PriceNotNegative))]
    public int SellPrice { get; set; }

    /// <summary>Inventory product one exam uses up (e.g. its test kit), which its reports can deduct; null = none.</summary>
    public int? ItemId { get; set; }

    /// <summary>The linked product's name, as the API sends it.</summary>
    public string? ItemName { get; set; }

    [Range(1, 1000, ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.QuantityPerExamRange))]
    public int ItemQuantity { get; set; } = 1;
}
