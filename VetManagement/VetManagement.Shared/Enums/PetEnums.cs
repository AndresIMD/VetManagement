namespace VetManagement.Shared.Enums;

/// <summary>
/// Biological sex of a pet.
/// </summary>
public enum Sex
{
    [DisplayString("Unknown")]
    Unknown,
    [DisplayString("Female")]
    Female,
    [DisplayString("Male")]
    Male
}

/// <summary>
/// Reproductive status of a pet.
/// </summary>
public enum ReproductiveStatus
{
    [DisplayString("Unknown")]
    Unknown,
    [DisplayString("Spayed")]
    Spayed,
    [DisplayString("Neutered")]
    Neutered,
    [DisplayString("Intact")]
    Intact
}

/// <summary>
/// Species classification of a pet.
/// </summary>
public enum Species
{
    [DisplayString("Unknown")]
    Unknown,
    [DisplayString("Dog")]
    Dog,
    [DisplayString("Cat")]
    Cat,
    [DisplayString("Bird")]
    Bird,
    [DisplayString("Fish")]
    Fish,
    [DisplayString("Reptile")]
    Reptile,
    [DisplayString("Rodent")]
    Rodent,
    [DisplayString("Other")]
    Other
}
