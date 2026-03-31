using VetManagement.Application.Contracts.Persistence;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Api.Services;

public class ClientPetService(IUnitOfWork unitOfWork)
{
    public async Task<List<Client>> GetAllClientsAsync()
        => await unitOfWork.Clients.GetAllAsync();

    public async Task<Client?> GetClientByIdAsync(int id)
        => await unitOfWork.Clients.GetByIdAsync(id);

    public async Task<Client?> GetClientByTaxIdAsync(string taxId)
        => await unitOfWork.Clients.GetByTaxIdAsync(taxId);

    public async Task AddClientAsync(Client client)
    {
        await unitOfWork.Clients.AddAsync(client);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateClientAsync(Client client)
    {
        unitOfWork.Clients.Update(client);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteClientAsync(int id)
    {
        var client = await unitOfWork.Clients.GetByIdAsync(id);
        if (client != null)
        {
            unitOfWork.Clients.Remove(client);
            await unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<List<Pet>> GetPetsByOwnerIdAsync(int ownerId)
    {
        var pets = await unitOfWork.Pets.GetByOwnerIdAsync(ownerId);
        return pets.ToList();
    }

    public async Task<Pet?> GetPetByIdAsync(int id)
        => await unitOfWork.Pets.GetByIdAsync(id);

    public async Task AddPetAsync(Pet pet)
    {
        await unitOfWork.Pets.AddAsync(pet);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task UpdatePetAsync(Pet pet)
    {
        unitOfWork.Pets.Update(pet);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task DeletePetAsync(int id)
    {
        var pet = await unitOfWork.Pets.GetByIdAsync(id);
        if (pet != null)
        {
            unitOfWork.Pets.Remove(pet);
            await unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Client>> SearchClientsAsync(string? searchTerm = null)
        => await unitOfWork.Clients.SearchAsync(searchTerm);

    public async Task<IEnumerable<Pet>> SearchPetsAsync(string? searchTerm = null, Species? species = null, ReproductiveStatus? status = null)
        => await unitOfWork.Pets.SearchAsync(searchTerm, species, status);
}
