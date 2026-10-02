using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Clients;

/// <summary>Body for creating or updating a client. Pets are managed through their own endpoints.</summary>
public sealed class ClientRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public string TaxId { get; init; } = string.Empty;

    [Required]
    public string Address { get; init; } = string.Empty;

    public int PhoneNumber { get; init; }

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Body for creating or updating a pet.</summary>
public sealed class PetRequest
{
    [Range(1, int.MaxValue)]
    public int OwnerId { get; init; }

    [Required]
    public string Name { get; init; } = string.Empty;

    public Sex Sex { get; init; }

    public Species Species { get; init; }

    [Required]
    public string Breed { get; init; } = string.Empty;

    public DateTime Birthdate { get; init; }

    public int Age { get; init; }

    public float Weight { get; init; }

    public ReproductiveStatus ReproductiveStatus { get; init; }

    public int ChipId { get; init; }
}

public sealed class ClientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int PhoneNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public List<PetDto> Pets { get; set; } = [];
}

public sealed class PetDto
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Sex Sex { get; set; }
    public Species Species { get; set; }
    public string Breed { get; set; } = string.Empty;
    public DateTime Birthdate { get; set; }
    public int Age { get; set; }
    public float Weight { get; set; }
    public ReproductiveStatus ReproductiveStatus { get; set; }
    public int ChipId { get; set; }
}
