using System.Net.Http.Json;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Shared.Services.Api;

public class ExamPerformedApiService(HttpClient http)
{
    public async Task<List<ExamPerformed>?> GetAllAsync()
        => await http.GetFromJsonAsync<List<ExamPerformed>>(ApiRouteConstants.EXAMS_PERFORMED_BASE);

    public async Task<ExamPerformed?> GetByIdAsync(int id)
        => await http.GetFromJsonAsync<ExamPerformed>(string.Format(ApiRouteConstants.EXAM_PERFORMED_BY_ID, id));

    public async Task AddAsync(ExamPerformed exam)
        => await http.PostAsJsonAsync(ApiRouteConstants.EXAMS_PERFORMED_BASE, exam);

    public async Task UpdateAsync(ExamPerformed exam)
        => await http.PutAsJsonAsync(string.Format(ApiRouteConstants.EXAM_PERFORMED_BY_ID, exam.Id), exam);

    public async Task DeleteAsync(int id)
        => await http.DeleteAsync(string.Format(ApiRouteConstants.EXAM_PERFORMED_BY_ID, id));
}
