using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// Lab exam offered by the clinic (catalog entry). Domain entity — no validation or UI attributes.
/// </summary>
public class Exam : Entity<int>
{
    public Exam() : base(0) { }

    public string Name { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Machine { get; set; } = string.Empty;

    public SampleType SampleType { get; set; }

    public SampleContainer SampleContainer { get; set; }

    public int BuyPrice { get; set; }

    public int SellPrice { get; set; }
}
