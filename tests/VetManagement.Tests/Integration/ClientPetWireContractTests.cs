using System.Text.Json;
using FluentAssertions;
using VetManagement.Contracts.Clients;
using VetManagement.Domain.Enums;
using UiClient = VetManagement.Shared.Models.Core.Client;
using UiPet = VetManagement.Shared.Models.Core.Pet;

namespace VetManagement.Tests.Integration;

/// <summary>
/// The staff UI posts its own view models and reads API DTOs. A renamed property on either side
/// would silently drop data, so this checks the JSON round-trip in both directions.
/// </summary>
public class ClientPetWireContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static TTo RoundTrip<TTo>(object from) =>
        JsonSerializer.Deserialize<TTo>(JsonSerializer.Serialize(from, Web), Web)!;

    private static UiPet SamplePet() => new()
    {
        Id = 7, OwnerId = 3, Name = "Luna", Sex = Sex.Female, Species = Species.Cat, Breed = "Siamese",
        Birthdate = new DateTime(2020, 5, 1), Age = 5, Weight = 4.2f,
        ReproductiveStatus = ReproductiveStatus.Spayed, ChipId = 1234567
    };

    [Fact]
    public void UiClient_BindsTo_ClientRequest()
    {
        var ui = new UiClient("Ana", "Lopez", "TAX-1", "Main St 1", 912345678, "ana@mail.com");

        RoundTrip<ClientRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void UiPet_BindsTo_PetRequest()
    {
        var ui = SamplePet();

        RoundTrip<PetRequest>(ui).Should().BeEquivalentTo(ui, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void ClientDto_ReadsInto_UiClient_WithPets()
    {
        var dto = new ClientDto
        {
            Id = 3, Name = "Ana", LastName = "Lopez", TaxId = "TAX-1", Address = "Main St 1",
            PhoneNumber = 912345678, Email = "ana@mail.com",
            Pets = [RoundTrip<PetDto>(SamplePet())]
        };

        RoundTrip<UiClient>(dto).Should().BeEquivalentTo(dto);
    }
}
