using System.Net;
using System.Net.Http.Json;
using VetManagement.Contracts.ClientPortal;

namespace VetManagement.Booking.Web.Services;

/// <summary>Client for the API's client portal endpoints (api/public/portal).</summary>
public class ClientPortalApi(HttpClient http)
{
    private const string Base = "api/public/portal";

    /// <summary>Null on success (the link goes by email), otherwise a message for the client.</summary>
    public async Task<string?> RequestLinkAsync(string rut)
    {
        var response = await http.PostAsJsonAsync($"{Base}/link", new PortalLinkRequest { Rut = rut });
        return response.StatusCode switch
        {
            HttpStatusCode.Accepted => null,
            HttpStatusCode.BadRequest => "El RUT no es válido.",
            HttpStatusCode.NotFound => "La ficha en línea no está disponible en esta clínica.",
            HttpStatusCode.TooManyRequests => "Demasiados intentos. Espera un minuto e inténtalo de nuevo.",
            _ => "No pudimos procesar tu solicitud. Inténtalo de nuevo."
        };
    }

    /// <summary>Null when the link is unknown or expired.</summary>
    public async Task<ClientPortalDto?> GetAsync(string token)
    {
        var response = await http.GetAsync($"{Base}/{Uri.EscapeDataString(token)}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ClientPortalDto>() : null;
    }
}
