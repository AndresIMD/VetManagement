using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Contracts.Scheduling;

namespace VetManagement.Booking.Web.Services;

/// <summary>Result of a write call, with a message ready to show to the client (Spanish).</summary>
public sealed record PortalResult<T>(T? Value, string? Error = null)
{
    public bool Ok => Error is null;
}

/// <summary>Client for the API's public booking endpoints (api/public/booking).</summary>
public class BookingApi(HttpClient http)
{
    private const string Base = "api/public/booking";

    public Task<PublicBookingInfoDto?> GetInfoAsync() => http.GetFromJsonAsync<PublicBookingInfoDto>($"{Base}/info");

    public async Task<List<AvailableSlotDto>> GetSlotsAsync(string serviceCode, DateOnly from, DateOnly to)
        => await http.GetFromJsonAsync<List<AvailableSlotDto>>($"{Base}/availability?service={Uri.EscapeDataString(serviceCode)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}") ?? [];

    public async Task<PortalResult<OnlineBookingResponseDto>> BookAsync(OnlineBookingRequestDto request)
    {
        var response = await http.PostAsJsonAsync(Base, request);
        if (response.IsSuccessStatusCode)
            return new(await response.Content.ReadFromJsonAsync<OnlineBookingResponseDto>());
        return new(null, response.StatusCode switch
        {
            HttpStatusCode.Conflict => "Esa hora acaba de ser tomada. Por favor elige otra.",
            HttpStatusCode.TooManyRequests => "Demasiados intentos. Espera un minuto e inténtalo de nuevo.",
            _ => await ProblemMessageAsync(response) ?? "No pudimos completar la reserva. Revisa tus datos e inténtalo de nuevo."
        });
    }

    public async Task<PublicBookingDto?> GetBookingAsync(string publicToken)
    {
        var response = await http.GetAsync($"{Base}/{Uri.EscapeDataString(publicToken)}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PublicBookingDto>() : null;
    }

    public async Task<PortalResult<bool>> CancelAsync(string publicToken)
    {
        var response = await http.PostAsync($"{Base}/{Uri.EscapeDataString(publicToken)}/cancel", null);
        return response.IsSuccessStatusCode
            ? new(true)
            : new(false, response.StatusCode == HttpStatusCode.Conflict
                ? "Ya no es posible cancelar en línea. Por favor contacta a la clínica."
                : "No pudimos cancelar la hora. Inténtalo de nuevo.");
    }

    /// <summary>Validation messages from the API (ProblemDetails), translated when we know them.</summary>
    private static async Task<string?> ProblemMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!problem.TryGetProperty("errors", out var errors))
                return null;
            var first = errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(v => v.GetString()).FirstOrDefault();
            return first switch
            {
                "The RUT is not valid." => "El RUT no es válido.",
                "The email address is not valid." => "El correo electrónico no es válido.",
                "The phone number is not valid." => "El teléfono no es válido.",
                "Online booking is not available." => "La reserva en línea no está disponible.",
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
