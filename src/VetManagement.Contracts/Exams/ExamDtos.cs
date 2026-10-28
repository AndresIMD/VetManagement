using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Exams;

/// <summary>Body for creating or updating a catalog exam.</summary>
public sealed class ExamRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Brand { get; init; } = string.Empty;

    [Required]
    public string Machine { get; init; } = string.Empty;

    public SampleType SampleType { get; init; }

    public SampleContainer SampleContainer { get; init; }

    [Range(0, int.MaxValue)]
    public int BuyPrice { get; init; }

    [Range(0, int.MaxValue)]
    public int SellPrice { get; init; }

    /// <summary>Inventory product one exam uses up (e.g. its test kit), deducted from the report; null = none. Needs the Inventory module.</summary>
    [Range(1, int.MaxValue)]
    public int? ItemId { get; init; }

    [Range(1, 1000)]
    public int ItemQuantity { get; init; } = 1;

    /// <summary>A product of its own for some species (e.g. the feline kit), instead of <see cref="ItemId"/>; one per species.</summary>
    public List<ExamSpeciesItemDto> SpeciesItems { get; init; } = [];
}

/// <summary>An exam's own inventory product for one species, and the units one exam uses.</summary>
public sealed class ExamSpeciesItemDto
{
    public Species Species { get; set; }

    [Range(1, int.MaxValue)]
    public int ItemId { get; set; }

    /// <summary>The product's name, as the API sends it; ignored when saving.</summary>
    public string? ItemName { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;
}

/// <summary>An inventory product the catalog can link to an exam, with its current stock.</summary>
public sealed class ExamStockItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Stock { get; set; }
}

/// <summary>Body for creating or updating an external lab.</summary>
public sealed class ExternalLabRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public string? ContactName { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? Address { get; init; }

    public string? Notes { get; init; }

    public int? DefaultTurnaroundDays { get; init; }
}

/// <summary>Body for creating or updating an exam order with its items.</summary>
public sealed class ExamPerformedRequest
{
    [Range(1, int.MaxValue)]
    public int PatientId { get; init; }

    [Required]
    public string Responsible { get; init; } = string.Empty;

    public DateTime Date { get; init; } = DateTime.UtcNow;

    public List<ExamRequestItemRequest> Items { get; init; } = [];
}

/// <summary>
/// One item of an exam order. <see cref="Id"/> identifies an existing item when updating (0 = new).
/// The lab is referenced by id only; labs are managed through their own endpoints.
/// </summary>
public sealed class ExamRequestItemRequest
{
    public int Id { get; init; }

    [Range(1, int.MaxValue)]
    public int ExamId { get; init; }

    public ExamItemStatus Status { get; init; } = ExamItemStatus.Pending;

    public bool IsExternal { get; init; }

    public int? ExternalEstimatedDays { get; init; }

    public int? ExternalLabId { get; init; }

    public Dictionary<string, string> Attributes { get; init; } = [];
}

public sealed class ExamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public SampleType SampleType { get; set; }
    public SampleContainer SampleContainer { get; set; }
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }
    /// <summary>The linked inventory product (null when none, or the Inventory module is off) and units per exam.</summary>
    public int? ItemId { get; set; }
    public string? ItemName { get; set; }
    public int ItemQuantity { get; set; } = 1;
    /// <summary>Products of their own for some species, instead of <see cref="ItemId"/> (empty with the Inventory module off).</summary>
    public List<ExamSpeciesItemDto> SpeciesItems { get; set; } = [];
}

public sealed class ExternalLabDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int? DefaultTurnaroundDays { get; set; }
}

public sealed class ExamPerformedDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string Responsible { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<ExamRequestItemDto> Items { get; set; } = [];
}

public sealed class ExamRequestItemDto
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public ExamItemStatus Status { get; set; }
    public bool IsExternal { get; set; }
    public int? ExternalEstimatedDays { get; set; }
    public int? ExternalLabId { get; set; }
    public ExternalLabDto? ExternalLab { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = [];
}
