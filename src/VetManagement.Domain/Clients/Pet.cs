using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Clients;

/// <summary>
/// Patient of the clinic, owned by a <see cref="Client"/>. Domain entity — no validation or UI attributes.
/// </summary>
public class Pet : Entity<int>
{
    public Pet() : base(0) { }

    public int OwnerId { get; set; }

    public required string Name { get; set; }

    public Sex Sex { get; set; }

    public Species Species { get; set; } = Species.Unknown;

    public required string Breed { get; set; }

    public DateTime Birthdate { get; set; }

    public int Age { get; set; }

    public float Weight { get; set; }

    public ReproductiveStatus ReproductiveStatus { get; set; }

    public int ChipId { get; set; }
}
