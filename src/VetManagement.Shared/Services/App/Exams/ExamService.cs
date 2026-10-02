using VetManagement.Domain.Enums;
using VetManagement.Shared.Helpers;
using VetManagement.Shared.Models.Exams;
using VetManagement.Shared.Services.Api;

namespace VetManagement.Shared.Services.App.Exams;

public class ExamService(IExamApiService api)
{
    public async Task<List<Exam>> GetAllAsync() => await api.GetAllExamsAsync() ?? new List<Exam>();

    public async Task<ExamValidationResult> ValidateAsync(Exam exam)
    {
        var r = new ExamValidationResult();
        if (exam is null)
        {
            r.Add("Exam is required.");
            return r;
        }
        ValidateString(exam.Name, 3, 120, "Name", r);
        ValidateString(exam.Brand, 2, 80, "Brand", r);
        ValidateString(exam.Machine, 2, 80, "Machine", r);
        if (exam.SampleType == SampleType.None)
            r.Add("Sample type is required.");
        if (exam.SampleContainer == SampleContainer.None)
            r.Add("Sample container is required.");
        if (exam.BuyPrice < 0)
            r.Add("Buy price must be >= 0.");
        if (exam.SellPrice < 0)
            r.Add("Sell price must be >= 0.");
        if (exam.SellPrice < exam.BuyPrice)
            r.Add("Sell price must be >= buy price.");
        var all = await GetAllAsync();
        var normalized = StringExtensions.RemoveDiacritics(exam.Name).ToLowerInvariant();
        if (all.Any(e => e.Id != exam.Id && StringExtensions.RemoveDiacritics(e.Name).ToLowerInvariant() == normalized))
            r.Add("An exam with the same name already exists.");
        return r;
    }

    public async Task<(ExamValidationResult Validation, Exam? Saved)> AddAsync(Exam exam)
    {
        var validation = await ValidateAsync(exam);
        if (!validation.IsValid)
            return (validation, null);
        await api.AddExamAsync(exam);
        return (validation, exam);
    }

    public async Task<(ExamValidationResult Validation, Exam? Updated)> UpdateAsync(Exam exam)
    {
        var validation = await ValidateAsync(exam);
        if (!validation.IsValid)
            return (validation, null);
        await api.UpdateExamAsync(exam);
        return (validation, exam);
    }

    public async Task DeleteAsync(int id) => await api.DeleteExamAsync(id);

    private static void ValidateString(string value, int min, int max, string field, ExamValidationResult r)
    {
        if (string.IsNullOrWhiteSpace(value))
        { r.Add($"{field} is required."); return; }
        var t = value.Trim();
        if (t.Length < min)
            r.Add($"{field} must be at least {min} characters.");
        if (t.Length > max)
            r.Add($"{field} must be at most {max} characters.");
    }
}
