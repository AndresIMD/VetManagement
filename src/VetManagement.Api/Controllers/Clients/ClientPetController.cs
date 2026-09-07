using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Enums;

namespace VetManagement.Api.Controllers.Clients;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ClientsPets.Read")]
public class ClientPetController(ClientPetService service) : ApiControllerBase
{
    #region Client Endpoints
    [HttpGet("clients")]
    public async Task<ActionResult<List<Client>>> GetClientsAsync()
        => Ok(await service.GetAllClientsAsync());

    [HttpGet("clients/{id:int}")]
    public async Task<IActionResult> GetClientAsync(int id)
    {
        var client = await service.GetClientByIdAsync(id);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpGet("clients/search")]
    public async Task<IActionResult> SearchClientsAsync([FromQuery] string? searchTerm = null)
        => Ok(await service.SearchClientsAsync(searchTerm));

    [HttpPost("clients")]
    [Authorize(Policy = "ClientsPets.Create")]
    public async Task<IActionResult> AddClientAsync([FromBody] Client client)
    {
        await service.AddClientAsync(client, GetUserName());
        return Ok();
    }

    [HttpPut("clients/{id:int}")]
    [Authorize(Policy = "ClientsPets.Update")]
    public async Task<IActionResult> UpdateClientAsync(int id, [FromBody] Client client)
    {
        if (id != client.Id)
            return BadRequest("ID mismatch.");
        await service.UpdateClientAsync(client, GetUserName());
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
    public async Task<ActionResult<List<Pet>>> GetPetsByOwnerAsync(int ownerId)
        => Ok(await service.GetPetsByOwnerIdAsync(ownerId));

    [HttpGet("pets/{id:int}")]
    public async Task<IActionResult> GetPetAsync(int id)
    {
        var pet = await service.GetPetByIdAsync(id);
        return pet is null ? NotFound() : Ok(pet);
    }

    [HttpGet("pets/search")]
    public async Task<IActionResult> SearchPetsAsync(
        [FromQuery] string? searchTerm = null,
        [FromQuery] Species? species = null,
        [FromQuery] ReproductiveStatus? status = null)
        => Ok(await service.SearchPetsAsync(searchTerm, species, status));

    [HttpPost("pets")]
    [Authorize(Policy = "ClientsPets.Create")]
    public async Task<IActionResult> AddPetAsync([FromBody] Pet pet)
    {
        await service.AddPetAsync(pet, GetUserName());
        return Ok();
    }

    [HttpPut("pets/{id:int}")]
    [Authorize(Policy = "ClientsPets.Update")]
    public async Task<IActionResult> UpdatePetAsync(int id, [FromBody] Pet pet)
    {
        if (id != pet.Id)
            return BadRequest("ID mismatch.");
        await service.UpdatePetAsync(pet, GetUserName());
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
}
