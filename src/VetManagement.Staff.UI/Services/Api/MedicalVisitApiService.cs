using System.Net.Http.Json;
using VetManagement.Staff.UI.Constants;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Models.Medical;

namespace VetManagement.Staff.UI.Services.Api;

public class MedicalVisitApiService(HttpClient http)
{
    public async Task<List<MedicalVisit>?> GetAllAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? patientId = null,
        string? responsible = null,
        PaymentStatus? paymentStatus = null,
        string? search = null)
    {
        var qs = new List<string>();
        if (from.HasValue)
            qs.Add($"from={from:O}");
        if (to.HasValue)
            qs.Add($"to={to:O}");
        if (patientId.HasValue)
            qs.Add($"patientId={patientId}");
        if (!string.IsNullOrWhiteSpace(responsible))
            qs.Add($"responsible={Uri.EscapeDataString(responsible)}");
        if (paymentStatus.HasValue)
            qs.Add($"paymentStatus={(int)paymentStatus.Value}");
        if (!string.IsNullOrWhiteSpace(search))
            qs.Add($"search={Uri.EscapeDataString(search)}");
        var url = ApiRouteConstants.MEDICAL_VISITS + (qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty);
        return await http.GetFromJsonAsync<List<MedicalVisit>>(url);
    }

    public async Task<MedicalVisit?> GetByIdAsync(int id)
        => await http.GetFromJsonAsync<MedicalVisit>(string.Format(ApiRouteConstants.MEDICAL_VISIT_BY_ID, id));

    public async Task AddAsync(MedicalVisit visit)
        => await http.PostAsJsonAsync(ApiRouteConstants.MEDICAL_VISITS, visit);

    public async Task UpdateAsync(MedicalVisit visit)
        => await http.PutAsJsonAsync(string.Format(ApiRouteConstants.MEDICAL_VISIT_BY_ID, visit.Id), visit);

    public async Task DeleteAsync(int id)
        => await http.DeleteAsync(string.Format(ApiRouteConstants.MEDICAL_VISIT_BY_ID, id));
}
