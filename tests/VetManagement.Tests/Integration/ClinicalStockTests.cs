using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Clinical;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>Drugs and materials used in visits, and vaccines applied from inventory, leave stock and come back when removed.</summary>
public class ClinicalStockTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _employee;
    private static readonly SemaphoreSlim SettingsLock = new(1, 1);

    public ClinicalStockTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _employee = factory.CreateClientWithPermissions([.. Permissions.ForRole("Employee")]);
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> AddItemAsync(int stock) => WithDbAsync(async db =>
    {
        var item = new Item($"Ketoprofeno {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, 5000);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    private Task<int> StockOfAsync(int itemId) => WithDbAsync(async db => (await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock);

    /// <summary>A pet with one visit; returns both ids.</summary>
    private async Task<(int PetId, int VisitId)> AddPetWithVisitAsync()
    {
        var petId = await WithDbAsync(async db =>
        {
            var owner = new Client("Ana", "Soto", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, "ana@example.test");
            db.Clients.Add(owner);
            await db.SaveChangesAsync();
            var pet = new Pet { OwnerId = owner.Id, Name = "Toby", Breed = "Mestizo", Species = Species.Dog };
            db.Pets.Add(pet);
            await db.SaveChangesAsync();
            return pet.Id;
        });
        (await _employee.PostAsJsonAsync("/api/medical-visits", new { PatientId = petId, PatientName = "Toby", Date = DateTime.UtcNow, Reason = "Cojera" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var visitId = (await HistoryAsync(petId)).Visits.Single().Id;
        return (petId, visitId);
    }

    private async Task<PetHistoryDto> HistoryAsync(int petId) => (await _admin.GetFromJsonAsync<PetHistoryDto>($"/api/clinical/pets/{petId}/history"))!;

    private static async Task<int> IdOf(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    [Fact]
    public async Task SupplyUsedInAVisit_LeavesStock_RemovingItPutsItBack()
    {
        var itemId = await AddItemAsync(stock: 10);
        var (petId, visitId) = await AddPetWithVisitAsync();

        var supplyId = await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 3 }));
        (await StockOfAsync(itemId)).Should().Be(7);
        (await WithDbAsync(db => db.InventoryMovements.CountAsync(m => m.ItemId == itemId && m.Reason == $"Visit #{visitId}"))).Should().Be(1);

        var supply = (await HistoryAsync(petId)).Supplies.Single();
        supply.VisitId.Should().Be(visitId);
        supply.Quantity.Should().Be(3);
        supply.StockDeducted.Should().BeTrue();

        (await _employee.DeleteAsync($"/api/clinical/supplies/{supplyId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await IdOf(await _admin.DeleteAsync($"/api/clinical/supplies/{supplyId}"));
        (await StockOfAsync(itemId)).Should().Be(10);
        (await HistoryAsync(petId)).Supplies.Should().BeEmpty();
    }

    [Fact]
    public async Task UsingMoreThanTheStock_IsRejected()
    {
        var itemId = await AddItemAsync(stock: 2);
        var (_, visitId) = await AddPetWithVisitAsync();

        var response = await _employee.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 3 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Only 2");
        (await StockOfAsync(itemId)).Should().Be(2);
    }

    [Fact]
    public async Task DeletingAVisit_PutsItsSuppliesBackInStock()
    {
        var itemId = await AddItemAsync(stock: 5);
        var (_, visitId) = await AddPetWithVisitAsync();
        await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 2 }));
        (await StockOfAsync(itemId)).Should().Be(3);

        (await _admin.DeleteAsync($"/api/medical-visits/{visitId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await StockOfAsync(itemId)).Should().Be(5);
        (await WithDbAsync(db => db.VisitSupplies.CountAsync(s => s.VisitId == visitId))).Should().Be(0);
    }

    [Fact]
    public async Task VaccineFromInventory_TakesOneUnit_DeletingTheDoseReturnsIt()
    {
        var itemId = await AddItemAsync(stock: 1);
        var (petId, _) = await AddPetWithVisitAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

        var doseId = await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "rabies", AppliedOn = today, ItemId = itemId }));
        (await StockOfAsync(itemId)).Should().Be(0);
        (await HistoryAsync(petId)).Doses.Single().ItemId.Should().Be(itemId);

        var outOfStock = await _employee.PostAsJsonAsync($"/api/clinical/pets/{petId}/doses", new { ProtocolCode = "rabies", AppliedOn = today, ItemId = itemId });
        outOfStock.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await outOfStock.Content.ReadAsStringAsync()).Should().Contain("out of stock");

        await IdOf(await _admin.DeleteAsync($"/api/clinical/doses/{doseId}"));
        (await StockOfAsync(itemId)).Should().Be(1);
    }

    [Fact]
    public async Task WhenTheClinicDoesNotTrackClinicalUse_SuppliesAreRecordedWithoutMovingStock()
    {
        var itemId = await AddItemAsync(stock: 1);
        var (petId, visitId) = await AddPetWithVisitAsync();

        await SettingsLock.WaitAsync();
        try
        {
            await SetDeductAsync(false);
            await IdOf(await _employee.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 4 }));
        }
        finally
        {
            await SetDeductAsync(true);
            SettingsLock.Release();
        }

        (await StockOfAsync(itemId)).Should().Be(1);
        (await HistoryAsync(petId)).Supplies.Single().StockDeducted.Should().BeFalse();
    }

    private async Task SetDeductAsync(bool deduct)
    {
        var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/clinical/settings"))!;
        doc["settings"]!["deductStockOnUse"] = deduct;
        (await _admin.PutAsJsonAsync("/api/clinical/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
