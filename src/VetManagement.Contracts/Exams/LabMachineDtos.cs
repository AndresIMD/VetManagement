using System.ComponentModel.DataAnnotations;

namespace VetManagement.Contracts.Exams;

/// <summary>A lab machine and how many catalog exams run on it.</summary>
public sealed record LabMachineDto(int Id, string Brand, string Model, int ExamCount);

/// <summary>Body for creating or renaming a lab machine.</summary>
public sealed class LabMachineRequest
{
    [Required, MaxLength(100)]
    public string Brand { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Model { get; init; } = string.Empty;
}
