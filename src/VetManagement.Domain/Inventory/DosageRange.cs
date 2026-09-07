namespace VetManagement.Domain.Inventory;

/// <summary>
/// Dosage range for a drug. Domain value object — no validation attributes.
/// </summary>
public sealed record DosageRange(float Min, float Max)
{
    public bool IsValid => Max >= Min;
}