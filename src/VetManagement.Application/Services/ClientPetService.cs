using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Application.Services;

public class ClientPetService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    #region Client Operations

    public async Task<List<Client>> GetAllClientsAsync()
        => await unitOfWork.Clients.GetAllAsync();

    public async Task<Client?> GetClientByIdAsync(int id)
        => await unitOfWork.Clients.GetByIdAsync(id);

    public async Task<Client?> GetClientByTaxIdAsync(string taxId)
        => await unitOfWork.Clients.GetByTaxIdAsync(taxId);

    public async Task<IEnumerable<Client>> SearchClientsAsync(string? searchTerm = null)
        => await unitOfWork.Clients.SearchAsync(searchTerm);

    public async Task AddClientAsync(Client client, string userName)
    {
        await unitOfWork.Clients.AddAsync(client);
        await unitOfWork.SaveChangesAsync();
        await LogClientAuditAsync(client.Id, AuditActionType.Add, JsonSerializer.Serialize(client), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Client>(client.Id, "Add");
            await notificationService.NotifyCollectionChangedAsync<Client>("Add");
        }
    }

    public async Task UpdateClientAsync(Client client, string userName)
    {
        unitOfWork.Clients.Update(client);
        await unitOfWork.SaveChangesAsync();
        await LogClientAuditAsync(client.Id, AuditActionType.Edit, JsonSerializer.Serialize(client), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Client>(client.Id, "Update");
        }
    }

    public async Task<bool> DeleteClientAsync(int id, string userName)
    {
        var client = await unitOfWork.Clients.GetByIdAsync(id);
        if (client == null)
            return false;

        var snapshot = JsonSerializer.Serialize(client);
        unitOfWork.Clients.Remove(client);
        await unitOfWork.SaveChangesAsync();
        await LogClientAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Client>(id, "Delete");
            await notificationService.NotifyCollectionChangedAsync<Client>("Delete");
        }

        return true;
    }

    #endregion

    #region Pet Operations

    public async Task<List<Pet>> GetPetsByOwnerIdAsync(int ownerId)
    {
        var pets = await unitOfWork.Pets.GetByOwnerIdAsync(ownerId);
        return pets.ToList();
    }

    public async Task<Pet?> GetPetByIdAsync(int id)
        => await unitOfWork.Pets.GetByIdAsync(id);

    public async Task<IEnumerable<Pet>> SearchPetsAsync(string? searchTerm = null, Species? species = null, ReproductiveStatus? status = null)
        => await unitOfWork.Pets.SearchAsync(searchTerm, species, status);

    public async Task AddPetAsync(Pet pet, string userName)
    {
        await unitOfWork.Pets.AddAsync(pet);
        await unitOfWork.SaveChangesAsync();
        await LogPetAuditAsync(pet.Id, AuditActionType.Add, JsonSerializer.Serialize(pet), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Pet>(pet.Id, "Add");
            await notificationService.NotifyCollectionChangedAsync<Pet>("Add");
        }
    }

    public async Task UpdatePetAsync(Pet pet, string userName)
    {
        unitOfWork.Pets.Update(pet);
        await unitOfWork.SaveChangesAsync();
        await LogPetAuditAsync(pet.Id, AuditActionType.Edit, JsonSerializer.Serialize(pet), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Pet>(pet.Id, "Update");
        }
    }

    public async Task<bool> DeletePetAsync(int id, string userName)
    {
        var pet = await unitOfWork.Pets.GetByIdAsync(id);
        if (pet == null)
            return false;

        var snapshot = JsonSerializer.Serialize(pet);
        unitOfWork.Pets.Remove(pet);
        await unitOfWork.SaveChangesAsync();
        await LogPetAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Pet>(id, "Delete");
            await notificationService.NotifyCollectionChangedAsync<Pet>("Delete");
        }

        return true;
    }

    #endregion

    #region Private Helpers

    private async Task LogClientAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(Client),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }

    private async Task LogPetAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(Pet),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }

    #endregion
}
