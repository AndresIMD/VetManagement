using System.Net.Http.Json;
using VetManagement.Staff.UI.Constants;
using VetManagement.Staff.UI.Models.Exams;

namespace VetManagement.Staff.UI.Services.Api;

public class ExamApiService(HttpClient http)
{
    public async Task<List<Exam>?> GetAllExamsAsync()
        => await http.GetFromJsonAsync<List<Exam>>(ApiRouteConstants.EXAMS);

    public async Task AddExamAsync(Exam exam)
        => (await http.PostAsJsonAsync(ApiRouteConstants.EXAMS, exam)).EnsureSuccessStatusCode();

    public async Task UpdateExamAsync(Exam exam)
        => (await http.PutAsJsonAsync(string.Format(ApiRouteConstants.EXAM_BY_ID, exam.Id), exam)).EnsureSuccessStatusCode();

    public async Task DeleteExamAsync(int id)
        => (await http.DeleteAsync(string.Format(ApiRouteConstants.EXAM_BY_ID, id))).EnsureSuccessStatusCode();
}
