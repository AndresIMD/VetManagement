using System.Net.Http.Json;
using VetManagement.Staff.UI.Constants;
using VetManagement.Staff.UI.Models.Core;

namespace VetManagement.Staff.UI.Services.Api;
public class ClientPetApiService(HttpClient http)
{
    public async Task<List<Client>?> GetAllClientsAsync()
        => await http.GetFromJsonAsync<List<Client>>(ApiRouteConstants.CLIENTS);

    public async Task<List<Pet>?> GetAllPetsAsync()
        => await http.GetFromJsonAsync<List<Pet>>(ApiRouteConstants.PETS);

    public async Task<Client?> GetClientByIdAsync(int clientId)
        => await http.GetFromJsonAsync<Client>(string.Format(ApiRouteConstants.CLIENT_BY_ID, clientId));

    public async Task<Pet?> GetPetByIdAsync(int petId)
        => await http.GetFromJsonAsync<Pet>(string.Format(ApiRouteConstants.PET_BY_ID, petId));

    public async Task AddClientAsync(Client client)
        => await http.PostAsJsonAsync(ApiRouteConstants.CLIENTS, client);

    public async Task AddPetAsync(Pet pet)
        => await http.PostAsJsonAsync(ApiRouteConstants.PETS, pet);

    public async Task UpdateClientAsync(Client client)
        => await http.PutAsJsonAsync(string.Format(ApiRouteConstants.CLIENT_BY_ID, client.Id), client);

    public async Task UpdatePetAsync(Pet pet)
        => await http.PutAsJsonAsync(string.Format(ApiRouteConstants.PET_BY_ID, pet.Id), pet);

    public async Task DeleteClientAsync(int clientId)
        => await http.DeleteAsync(string.Format(ApiRouteConstants.CLIENT_BY_ID, clientId));

    public async Task DeletePetAsync(int petId)
        => await http.DeleteAsync(string.Format(ApiRouteConstants.PET_BY_ID, petId));
}
