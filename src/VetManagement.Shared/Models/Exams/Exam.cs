using System.ComponentModel.DataAnnotations;
using VetManagement.Shared.Constants;
using VetManagement.Domain.Enums;

namespace VetManagement.Shared.Models.Exams;

public class Exam
{
    public int Id { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Machine { get; set; } = string.Empty;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public SampleType SampleType { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public SampleContainer SampleContainer { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "The price cannot be less than 0.")]
    public int BuyPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "The price cannot be less than 0.")]
    public int SellPrice { get; set; }
}
