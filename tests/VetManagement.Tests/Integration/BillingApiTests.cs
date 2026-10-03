using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Billing;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>Charging at the front desk, against the API with the default billing settings.</summary>
public class BillingApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _employee;

    public BillingApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        _employee = factory.CreateClientWithPermissions([.. Permissions.ForRole("Employee")]);
    }

    internal static async Task<int> IdOf(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    internal static async Task<int> OpenSaleAsync(HttpClient client, string name = "Ana Lopez")
        => await IdOf(await client.PostAsJsonAsync("/api/billing/sales", new { CustomerName = name }));

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> AddItemAsync(int stock, int price) => WithDbAsync(async db =>
    {
        var item = new Item($"Collar {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, price);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    private Task<int> StockOfAsync(int itemId) => WithDbAsync(async db => (await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock);

    private async Task<SaleDto> GetSaleAsync(int id) => (await _admin.GetFromJsonAsync<SaleDto>($"/api/billing/sales/{id}"))!;

    [Fact]
    public async Task CounterSale_SplitPayment_MarksPaidAndTakesProductsOutOfStock_VoidPutsThemBack()
    {
        var itemId = await AddItemAsync(stock: 5, price: 3000);
        var saleId = await OpenSaleAsync(_employee);

        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Product, ItemId = itemId, Quantity = 2 }));
        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Service, ServiceCode = "consultation" }));

        var sale = await GetSaleAsync(saleId);
        sale.Total.Should().Be(2 * 3000 + 25000, "catalog prices fill empty prices");
        sale.Lines.Select(l => l.Description).Should().Contain("Consulta general");

        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Cash", Amount = 10000 }));
        (await StockOfAsync(itemId)).Should().Be(5, "stock moves only when the sale is fully paid");
        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Debit", Amount = 21000, Reference = "VOUCHER-1" }));

        sale = await GetSaleAsync(saleId);
        sale.Status.Should().Be(SaleStatus.Paid);
        sale.Balance.Should().Be(0);
        (await StockOfAsync(itemId)).Should().Be(3);
        (await WithDbAsync(db => db.InventoryMovements.CountAsync(m => m.ItemId == itemId && m.Reason == $"Sale #{saleId}"))).Should().Be(1);

        (await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/void", new { Reason = "Error" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/void", new { Reason = "Cobro duplicado" }));

        (await GetSaleAsync(saleId)).Status.Should().Be(SaleStatus.Voided);
        (await StockOfAsync(itemId)).Should().Be(5);
    }

    [Fact]
    public async Task Payments_AboveTheBalance_OrWithADisabledMethod_AreRejected()
    {
        var saleId = await OpenSaleAsync(_employee);
        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Other, Description = "Certificado", UnitPrice = 5000 }));

        var over = await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Cash", Amount = 5001 });
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await over.Content.ReadAsStringAsync()).Should().Contain("exceeds the balance");

        (await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Bitcoin", Amount = 100 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = SalePayment.OnlineDepositMethod, Amount = 100 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "online deposits only come from the booking payment");
    }

    [Fact]
    public async Task DiscountAboveTheClinicLimit_NeedsBillingManagement()
    {
        var saleId = await OpenSaleAsync(_employee);
        var line = new { Kind = SaleLineKind.Other, Description = "Cirugía", UnitPrice = 100000, Discount = 20000 };

        var denied = await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", line);
        denied.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await denied.Content.ReadAsStringAsync()).Should().Contain("above 10%");

        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", line with { Discount = 10000 }));
        await IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", line));
    }

    [Fact]
    public async Task ProductLine_CannotSellMoreThanTheStock()
    {
        var itemId = await AddItemAsync(stock: 2, price: 1000);
        var saleId = await OpenSaleAsync(_employee);

        await IdOf(await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Product, ItemId = itemId, Quantity = 2 }));
        var tooMany = await _employee.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Product, ItemId = itemId, Quantity = 1 });

        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).Should().Contain("Only 0");
    }

    [Fact]
    public async Task SaleFromAppointment_StartsWithTheServiceAndTheDepositPaidOnline_OnlyOnce()
    {
        var appointmentId = await WithDbAsync(async db =>
        {
            var appointment = new Appointment
            {
                ServiceCode = "consultation", ResourceCode = "vet1", OwnerName = "Pedro Soto", PetName = "Toby",
                StartUtc = DateTime.UtcNow, EndUtc = DateTime.UtcNow.AddMinutes(30), OccupiedUntilUtc = DateTime.UtcNow.AddMinutes(30),
                Price = 25000, DepositAmount = 12500, DepositStatus = DepositStatus.Paid, PaidAmount = 12500, PaymentProvider = "Simulated",
                Status = AppointmentStatus.Confirmed, CreatedAtUtc = DateTime.UtcNow
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return appointment.Id;
        });

        var saleId = await IdOf(await _employee.PostAsJsonAsync("/api/billing/sales", new { AppointmentId = appointmentId }));
        var sale = await GetSaleAsync(saleId);

        sale.CustomerName.Should().Be("Pedro Soto");
        sale.Total.Should().Be(25000);
        sale.Payments.Should().ContainSingle(p => p.Method == SalePayment.OnlineDepositMethod && p.Amount == 12500);
        sale.Balance.Should().Be(12500);

        var again = await _employee.PostAsJsonAsync("/api/billing/sales", new { AppointmentId = appointmentId });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>().Should().Be(saleId);
    }

    [Fact]
    public async Task Settings_AreValidatedAndOnlyAdminsChangeThem()
    {
        var doc = (await _admin.GetFromJsonAsync<JsonObject>("/api/billing/settings"))!;
        doc["settings"]!["maxDiscountPercent"]!.GetValue<int>().Should().Be(10);

        (await _employee.PutAsJsonAsync("/api/billing/settings", doc)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var invalid = doc.DeepClone();
        invalid["settings"]!["maxDiscountPercent"] = 150;
        (await _admin.PutAsJsonAsync("/api/billing/settings", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _admin.PutAsJsonAsync("/api/billing/settings", doc)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PutAsJsonAsync("/api/billing/settings", doc)).StatusCode.Should().Be(HttpStatusCode.Conflict, "the version is stale");
    }
}

/// <summary>Closing today's cash freezes the day, so it runs on its own database.</summary>
public class CashCloseTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

    [Fact]
    public async Task DayClose_ComparesCountedCash_ThenFreezesTheDay()
    {
        var saleId = await BillingApiTests.OpenSaleAsync(_admin);
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines", new { Kind = SaleLineKind.Other, Description = "Baño", UnitPrice = 30000 }));
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Cash", Amount = 10000 }));
        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Credit", Amount = 5000 }));

        var sale = (await _admin.GetFromJsonAsync<SaleDto>($"/api/billing/sales/{saleId}"))!;
        var day = (await _admin.GetFromJsonAsync<DaySummaryDto>($"/api/billing/cash/{sale.BusinessDate:yyyy-MM-dd}"))!;
        day.ExpectedCash.Should().Be(10000);
        day.Totals.Should().Contain(t => t.Method == "Credit" && t.Amount == 5000 && !t.IsCash);
        day.IsClosed.Should().BeFalse();

        await BillingApiTests.IdOf(await _admin.PostAsJsonAsync("/api/billing/cash/close", new { Date = sale.BusinessDate, CountedCash = 9500, Notes = "Faltan 500" }));
        day = (await _admin.GetFromJsonAsync<DaySummaryDto>($"/api/billing/cash/{sale.BusinessDate:yyyy-MM-dd}"))!;
        day.IsClosed.Should().BeTrue();
        day.Difference.Should().Be(-500);

        (await _admin.PostAsJsonAsync("/api/billing/cash/close", new { Date = sale.BusinessDate, CountedCash = 1 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Cash", Amount = 1000 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/void", new { Reason = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PostAsJsonAsync("/api/billing/cash/close", new { Date = sale.BusinessDate.AddDays(1), CountedCash = 0 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
