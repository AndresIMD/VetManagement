using System.ComponentModel.DataAnnotations;

namespace VetManagement.Shared.Models.Core;

public class DosageRange(float min, float max) : IValidatableObject
{

    [Range(0, float.MaxValue, ErrorMessage = "The minimum dose must be 0 or greater.")]
    public float Min { get; set; } = min;
    public float Max { get; set; } = max;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Max < Min)
        {
            yield return new ValidationResult(
                "The maximum dose must be greater than or equal to the minimum dose.",
                [nameof(Max)]);
        }
    }
}
