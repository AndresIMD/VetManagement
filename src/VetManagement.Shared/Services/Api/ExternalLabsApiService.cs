using System.Net.Http.Json;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Shared.Services.Api;

public class ExternalLabsApiService(HttpClient http)
{
    public Task<List<ExternalLab>?> GetAllAsync()
        => http.GetFromJsonAsync<List<ExternalLab>>(ApiRouteConstants.EXTERNAL_LABS_BASE);

    public async Task<ExternalLab?> AddAsync(ExternalLab lab)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.EXTERNAL_LABS_BASE, lab);
        // A better implementation would be for the API to return the created object.
        return response.IsSuccessStatusCode ? lab : null;
    }

    public Task UpdateAsync(ExternalLab lab)
        => http.PutAsJsonAsync(string.Format(ApiRouteConstants.EXTERNAL_LAB_BY_ID, lab.Id), lab);

    public Task DeleteAsync(int id)
        => http.DeleteAsync(string.Format(ApiRouteConstants.EXTERNAL_LAB_BY_ID, id));
}
