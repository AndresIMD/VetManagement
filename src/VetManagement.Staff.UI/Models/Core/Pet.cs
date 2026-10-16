using System.ComponentModel.DataAnnotations;
using VetManagement.Staff.UI.Constants;
using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Models.Core;

public class Pet
{
    // -- Placeholder data for breeds by species --
    public static Dictionary<Species, List<string>> BreedsBySpecies { get; } = new()
    {
        { Species.Dog, new List<string> { "Labrador Retriever", "German Shepherd", "Golden Retriever", "Bulldog", "Beagle" } },
        { Species.Cat, new List<string> { "Persian", "Maine Coon", "Siamese", "Ragdoll", "Bengal" } },
        { Species.Bird, new List<string> { "Parakeet", "Canary", "Finch", "Cockatiel", "Parrot" } },
        { Species.Fish, new List<string> { "Goldfish", "Betta", "Guppy", "Angelfish", "Tetra" } },
        { Species.Reptile, new List<string> { "Bearded Dragon", "Leopard Gecko", "Corn Snake", "Ball Python", "Chameleon" } },
        { Species.Rodent, new List<string> { "Hamster", "Guinea Pig", "Mouse", "Rat", "Gerbil" } },
        { Species.Other, new List<string> { "Other" } }
    };

    public int Id { get; set; }

    public int OwnerId { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public required string Name { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public Sex Sex { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public Species Species { get; set; } = Species.Unknown;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public required string Breed { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public DateTime Birthdate { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public int Age { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public float Weight { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public ReproductiveStatus ReproductiveStatus { get; set; }

    public int ChipId { get; set; }

    /// <summary>
    /// Retrieves a list of breeds for a given species.
    /// </summary>
    /// <param name="species">The species for which to get breeds.</param>
    /// <returns>A list of breed names.</returns>
    public static List<string> GetBreedsBySpecies(Species species)
    {
        return BreedsBySpecies.TryGetValue(species, out var bySpecies) ? bySpecies : new List<string>();
    }
}
