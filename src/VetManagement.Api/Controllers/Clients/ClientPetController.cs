using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Contracts.Clients;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Controllers.Clients;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ClientsPets.Read")]
public class ClientPetController(ClientPetService service) : ApiControllerBase
{
    #region Client Endpoints
    [HttpGet("clients")]
    public async Task<ActionResult<List<ClientDto>>> GetClientsAsync()
        => Ok((await service.GetAllClientsAsync()).Select(MapToDto).ToList());

    [HttpGet("clients/{id:int}")]
    public async Task<IActionResult> GetClientAsync(int id)
    {
        var client = await service.GetClientByIdAsync(id);
        return client is null ? NotFound() : Ok(MapToDto(client));
    }

    [HttpGet("clients/search")]
    public async Task<IActionResult> SearchClientsAsync([FromQuery] string? searchTerm = null)
        => Ok((await service.SearchClientsAsync(searchTerm)).Select(MapToDto).ToList());

    [HttpPost("clients")]
    [Authorize(Policy = "ClientsPets.Create")]
    public async Task<IActionResult> AddClientAsync([FromBody] ClientRequest request)
    {
        await service.AddClientAsync(MapToDomain(request, id: 0), GetUserName());
        return Ok();
    }

    [HttpPut("clients/{id:int}")]
    [Authorize(Policy = "ClientsPets.Update")]
    public async Task<IActionResult> UpdateClientAsync(int id, [FromBody] ClientRequest request)
    {
        await service.UpdateClientAsync(MapToDomain(request, id), GetUserName());
        return Ok();
    }

    [HttpDelete("clients/{id:int}")]
    [Authorize(Policy = "ClientsPets.Delete")]
    public async Task<IActionResult> DeleteClientAsync(int id)
    {
        var result = await service.DeleteClientAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }

    #endregion

    #region Pet Endpoints
    [HttpGet("pets/owner/{ownerId:int}")]
    public async Task<ActionResult<List<PetDto>>> GetPetsByOwnerAsync(int ownerId)
        => Ok((await service.GetPetsByOwnerIdAsync(ownerId)).Select(MapToDto).ToList());

    [HttpGet("pets/{id:int}")]
    public async Task<IActionResult> GetPetAsync(int id)
    {
        var pet = await service.GetPetByIdAsync(id);
        return pet is null ? NotFound() : Ok(MapToDto(pet));
    }

    [HttpGet("pets/search")]
    public async Task<IActionResult> SearchPetsAsync(
        [FromQuery] string? searchTerm = null,
        [FromQuery] Species? species = null,
        [FromQuery] ReproductiveStatus? status = null)
        => Ok((await service.SearchPetsAsync(searchTerm, species, status)).Select(MapToDto).ToList());

    [HttpPost("pets")]
    [Authorize(Policy = "ClientsPets.Create")]
    public async Task<IActionResult> AddPetAsync([FromBody] PetRequest request)
    {
        await service.AddPetAsync(MapToDomain(request, id: 0), GetUserName());
        return Ok();
    }

    [HttpPut("pets/{id:int}")]
    [Authorize(Policy = "ClientsPets.Update")]
    public async Task<IActionResult> UpdatePetAsync(int id, [FromBody] PetRequest request)
    {
        await service.UpdatePetAsync(MapToDomain(request, id), GetUserName());
        return Ok();
    }

    [HttpDelete("pets/{id:int}")]
    [Authorize(Policy = "ClientsPets.Delete")]
    public async Task<IActionResult> DeletePetAsync(int id)
    {
        var result = await service.DeletePetAsync(id, GetUserName());
        if (!result) return NotFound();
        return Ok();
    }
    #endregion

    #region Mapping
    private static ClientDto MapToDto(Client client) => new()
    {
        Id = client.Id,
        Name = client.Name,
        LastName = client.LastName,
        TaxId = client.TaxId,
        Address = client.Address,
        PhoneNumber = client.PhoneNumber,
        Email = client.Email,
        Pets = client.Pets.Select(MapToDto).ToList()
    };

    private static PetDto MapToDto(Pet pet) => new()
    {
        Id = pet.Id,
        OwnerId = pet.OwnerId,
        Name = pet.Name,
        Sex = pet.Sex,
        Species = pet.Species,
        Breed = pet.Breed,
        Birthdate = pet.Birthdate,
        Age = pet.Age,
        Weight = pet.Weight,
        ReproductiveStatus = pet.ReproductiveStatus,
        ChipId = pet.ChipId
    };

    // The id comes from the route, never from the body (prevents over-posting).
    private static Client MapToDomain(ClientRequest request, int id) =>
        new(request.Name, request.LastName, request.TaxId, request.Address, request.PhoneNumber, request.Email) { Id = id };

    private static Pet MapToDomain(PetRequest request, int id) => new()
    {
        Id = id,
        OwnerId = request.OwnerId,
        Name = request.Name,
        Sex = request.Sex,
        Species = request.Species,
        Breed = request.Breed,
        Birthdate = request.Birthdate,
        Age = request.Age,
        Weight = request.Weight,
        ReproductiveStatus = request.ReproductiveStatus,
        ChipId = request.ChipId
    };
    #endregion
}
