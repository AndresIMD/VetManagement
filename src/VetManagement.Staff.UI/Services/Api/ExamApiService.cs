using System.Net.Http.Json;
using VetManagement.Contracts.Exams;
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

    public async Task<List<LabMachineDto>> GetLabMachinesAsync()
        => await http.GetFromJsonAsync<List<LabMachineDto>>(ApiRouteConstants.LAB_MACHINES) ?? [];

    public async Task<ApiResult> AddLabMachineAsync(LabMachineRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.LAB_MACHINES, request));

    /// <summary>On success the body's <c>examsUpdated</c> says how many catalog exams took the new name.</summary>
    public async Task<ApiResult> UpdateLabMachineAsync(int id, LabMachineRequest request)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(string.Format(ApiRouteConstants.LAB_MACHINE_BY_ID, id), request));

    public async Task<ApiResult> DeleteLabMachineAsync(int id)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.LAB_MACHINE_BY_ID, id)));
}
