using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// Exam order for a patient, made of one or more <see cref="ExamRequestItem"/>.
/// </summary>
public class ExamPerformed : Entity<int>
{
    public ExamPerformed() : base(0) { }

    public int PatientId { get; set; }

    public string Responsible { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public List<ExamRequestItem> Items { get; set; } = [];
}
