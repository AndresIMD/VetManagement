using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// Lab analyser the clinic runs exams on: a brand and one of its models (e.g. "IDEXX" and "Catalyst One").
/// The exam catalog picks from this list; an exam keeps the brand and model as text.
/// </summary>
public class LabMachine : Entity<int>
{
    public LabMachine() : base(0) { }

    public string Brand { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
