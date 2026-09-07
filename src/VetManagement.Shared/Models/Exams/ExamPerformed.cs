namespace VetManagement.Shared.Models.Exams;

/// <summary>
/// I use this to represent a set of exam items performed for a patient.
/// It groups the requested exam items and their results/status.
/// </summary>
public class ExamPerformed
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public string Responsible { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public List<ExamRequestItem> Items { get; set; } = new List<ExamRequestItem>();
}
