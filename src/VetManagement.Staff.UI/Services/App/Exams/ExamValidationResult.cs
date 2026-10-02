namespace VetManagement.Staff.UI.Services.App.Exams;

public class ExamValidationResult
{
    private readonly List<string> _errors = new();
    public IReadOnlyCollection<string> Errors => _errors.AsReadOnly();
    public bool IsValid => _errors.Count == 0;
    public void Add(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _errors.Add(message);
    }
}
