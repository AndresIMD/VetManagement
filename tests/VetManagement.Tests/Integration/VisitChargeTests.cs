using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Billing;
using VetManagement.Contracts.Clinical;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>"Charge visit": the appointment service, procedures and supplies of a visit in one sale, or split taxed / VAT-exempt.</summary>
public class VisitChargeTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private static readonly SemaphoreSlim SettingsReady = new(1, 1);
    private static bool _configured;

    public VisitChargeTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
    }

    /// <summary>The consultation is VAT-exempt for this clinic; procedures and products are taxed.</summary>
    private async Task ConfigureTaxAsync()
    {
        await SettingsReady.WaitAsync();
        try
        {
            if (_configured)
                return;
            var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/billing/settings"))!;
            doc["settings"]!["tax"]!["exemptServiceCodes"] = new JsonArray("consultation");
            (await _admin.PutAsJsonAsync("/api/billing/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
            _configured = true;
        }
        finally
        {
            SettingsReady.Release();
        }
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>A visit from an online appointment (deposit paid), with one procedure and one supply.</summary>
    private async Task<(int VisitId, int AppointmentId, int ItemId, int PetId)> VisitAsync()
    {
        var (petId, appointmentId, itemId) = await WithDbAsync(async db =>
        {
            var owner = new Client("Sofía", "Muñoz", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 3", 911111111, "sofia@example.test");
            db.Clients.Add(owner);
            await db.SaveChangesAsync();
            var pet = new Pet { OwnerId = owner.Id, Name = "Kiara", Breed = "Mestizo", Species = Species.Dog };
            db.Pets.Add(pet);
            var item = new Item($"Meloxicam {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock: 10, sellPrice: 3000);
            db.Items.Add(item);
            await db.SaveChangesAsync();
            var appointment = new Appointment
            {
                ServiceCode = "consultation", ResourceCode = "vet-general", ClientId = owner.Id, PetId = pet.Id, OwnerName = "Sofía Muñoz",
                StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddMinutes(30), OccupiedUntilUtc = DateTime.UtcNow.AddMinutes(30),
                Status = AppointmentStatus.Confirmed, Source = AppointmentSource.Online, Price = 25000, DepositAmount = 12500,
                DepositStatus = DepositStatus.Paid, PaidAmount = 12500, CreatedAtUtc = DateTime.UtcNow
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return (pet.Id, appointment.Id, item.Id);
        });

        (await _admin.PostAsJsonAsync("/api/medical-visits", new
        {
            PatientId = petId, PatientName = "Kiara", Date = DateTime.UtcNow, AppointmentId = appointmentId, Reason = "Cojera",
            Procedures = new[] { new { Name = "Radiografía", Price = 18000 } }
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var history = (await _admin.GetFromJsonAsync<PetHistoryDto>($"/api/clinical/pets/{petId}/history"))!;
        var visitId = history.Visits.Single().Id;
        (await _admin.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 2 })).StatusCode.Should().Be(HttpStatusCode.OK);
        return (visitId, appointmentId, itemId, petId);
    }

    private static List<object> AllLines(VisitChargePreviewDto preview)
        => preview.Lines.Select(l => (object)new { l.Source, l.TaxExempt }).ToList();

    [Fact]
    public async Task ChargeVisit_SplitsTaxedAndExempt_CarriesTheDeposit_AndDoesNotMoveStockAgain()
    {
        await ConfigureTaxAsync();
        var (visitId, appointmentId, itemId, petId) = await VisitAsync();

        var preview = (await _admin.GetFromJsonAsync<VisitChargePreviewDto>($"/api/billing/visits/{visitId}/charge"))!;
        preview.CustomerName.Should().Be("Sofía Muñoz");
        preview.OnlineDeposit.Should().Be(12500);
        preview.SplitByTaxDefault.Should().BeTrue();
        preview.Lines.Should().HaveCount(3);
        preview.Lines.Single(l => l.Source == "appointment").TaxExempt.Should().BeTrue();
        preview.Lines.Single(l => l.Source.StartsWith("procedure:")).Should().Match<VisitChargeLineDto>(l => l.UnitPrice == 18000 && !l.TaxExempt);
        preview.Lines.Single(l => l.Source.StartsWith("supply:")).Should().Match<VisitChargeLineDto>(l => l.Quantity == 2 && l.UnitPrice == 3000);

        var response = await _admin.PostAsJsonAsync($"/api/billing/visits/{visitId}/charge", new { Lines = AllLines(preview), SplitByTax = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saleIds = (await response.Content.ReadFromJsonAsync<ChargeVisitResponse>())!.SaleIds;
        saleIds.Should().HaveCount(2);

        var sales = new List<SaleDto>();
        foreach (var id in saleIds)
            sales.Add((await _admin.GetFromJsonAsync<SaleDto>($"/api/billing/sales/{id}"))!);
        var exempt = sales.Single(s => s.ExemptTotal > 0);
        var taxed = sales.Single(s => s.TaxableTotal > 0);

        exempt.Total.Should().Be(25000);
        exempt.Payments.Should().ContainSingle(p => p.Method == SalePayment.OnlineDepositMethod && p.Amount == 12500);
        exempt.Balance.Should().Be(12500);
        exempt.Vat.Should().Be(0);
        taxed.Total.Should().Be(18000 + 6000);
        taxed.TaxableNet.Should().Be(20168, "24.000 includes 19% VAT");
        taxed.Vat.Should().Be(3832);
        sales.Should().OnlyContain(s => s.VisitId == visitId && s.PetId == petId);

        // The supply left stock at the visit (10 → 8); paying the sale doesn't take it again.
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{taxed.Id}/payments", new { Method = "Cash", Amount = taxed.Balance }));
        (await WithDbAsync(db => db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId))).Stock.Should().Be(8);

        // Already charged: from the visit and from the agenda alike.
        var again = await _admin.PostAsJsonAsync($"/api/billing/visits/{visitId}/charge", new { Lines = AllLines(preview), SplitByTax = false });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync("/api/billing/sales", new { AppointmentId = appointmentId })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.GetFromJsonAsync<VisitChargePreviewDto>($"/api/billing/visits/{visitId}/charge"))!.ExistingSaleIds.Should().BeEquivalentTo(saleIds);
    }

    [Fact]
    public async Task ChargeVisit_InOneSale_WithEditedPriceAndTaxType()
    {
        await ConfigureTaxAsync();
        var (visitId, _, _, _) = await VisitAsync();
        var preview = (await _admin.GetFromJsonAsync<VisitChargePreviewDto>($"/api/billing/visits/{visitId}/charge"))!;
        var procedure = preview.Lines.Single(l => l.Source.StartsWith("procedure:"));

        // Only the procedure, at another price, marked exempt.
        var response = await _admin.PostAsJsonAsync($"/api/billing/visits/{visitId}/charge",
            new { Lines = new[] { new { procedure.Source, UnitPrice = 15000, TaxExempt = true } }, SplitByTax = false });
        var sale = (await _admin.GetFromJsonAsync<SaleDto>($"/api/billing/sales/{(await response.Content.ReadFromJsonAsync<ChargeVisitResponse>())!.SaleIds.Single()}"))!;

        sale.Total.Should().Be(15000);
        sale.ExemptTotal.Should().Be(15000);
        sale.Lines.Single().TaxExempt.Should().BeTrue();
        sale.Payments.Should().BeEmpty("the deposit belongs with the appointment, which wasn't charged");

        var bogus = await _admin.PostAsJsonAsync($"/api/billing/visits/{visitId + 1000}/charge",
            new { Lines = new[] { new { Source = "procedure:1", TaxExempt = false } }, SplitByTax = false });
        bogus.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ManualServiceLine_FollowsTheClinicsExemptList()
    {
        await ConfigureTaxAsync();
        var saleId = await BillingApiTests.OpenSaleAsync(_admin);
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Service, ServiceCode = "consultation" }));
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Service, ServiceCode = "vaccination" }));

        var sale = (await _admin.GetFromJsonAsync<SaleDto>($"/api/billing/sales/{saleId}"))!;
        sale.Lines.Single(l => l.ServiceCode == "consultation").TaxExempt.Should().BeTrue();
        sale.Lines.Single(l => l.ServiceCode == "vaccination").TaxExempt.Should().BeFalse();
    }
}
