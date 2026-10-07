using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

/// <summary>Report settings with the version they were read at (saving with a stale version answers 409).</summary>
public sealed record ExamReportSettingsState(ExamReportSettings Settings, int Version);

public class ExamReportsApiService(HttpClient http)
{
    // Same format as the API's settings documents (camelCase, enums by name).
    private static readonly JsonSerializerOptions SettingsOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<List<ExamReportDto>> ListAsync(int? petId)
        => await http.GetFromJsonAsync<List<ExamReportDto>>(petId is int id ? $"{ApiRouteConstants.EXAM_REPORTS}?petId={id}" : ApiRouteConstants.EXAM_REPORTS) ?? [];

    public async Task<ExamReportDto?> GetAsync(int id)
    {
        var response = await http.GetAsync(string.Format(ApiRouteConstants.EXAM_REPORT_BY_ID, id));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ExamReportDto>() : null;
    }

    public async Task<ApiResult> CreateAsync(ExamReportRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.EXAM_REPORTS, request));

    public async Task<ApiResult> UpdateAsync(int id, ExamReportRequest request)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(string.Format(ApiRouteConstants.EXAM_REPORT_BY_ID, id), request));

    public async Task<ApiResult> DeleteAsync(int id)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.EXAM_REPORT_BY_ID, id)));

    public async Task<ExamReportSettingsState> GetSettingsAsync()
    {
        var document = await http.GetFromJsonAsync<ExamReportSettingsDocument>(ApiRouteConstants.EXAM_REPORT_SETTINGS)
            ?? throw new InvalidOperationException("The report settings could not be read.");
        return new ExamReportSettingsState(document.Settings.Deserialize<ExamReportSettings>(SettingsOptions)!, document.Version);
    }

    public async Task<ApiResult> SaveSettingsAsync(ExamReportSettings settings, int version)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(ApiRouteConstants.EXAM_REPORT_SETTINGS, new ExamReportSettingsDocument
        {
            Version = version,
            Settings = JsonSerializer.SerializeToElement(settings, SettingsOptions)
        }));

    /// <summary>The image as a data URL for an &lt;img&gt;, or null when none was uploaded.</summary>
    public async Task<string?> GetImageDataUrlAsync(ClinicImageKind kind)
    {
        var response = await http.GetAsync(string.Format(ApiRouteConstants.EXAM_REPORT_IMAGE, kind));
        if (!response.IsSuccessStatusCode)
            return null;
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return $"data:{response.Content.Headers.ContentType?.MediaType};base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<ApiResult> SetImageAsync(ClinicImageKind kind, string contentType, byte[] data)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(string.Format(ApiRouteConstants.EXAM_REPORT_IMAGE, kind), new ClinicImageRequest { ContentType = contentType, Data = data }));

    public async Task<ApiResult> DeleteImageAsync(ClinicImageKind kind)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.EXAM_REPORT_IMAGE, kind)));
}
