using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>
/// The migration creates the daily-use warehouse with the stock as it was, and the queries that leave out what a user
/// doesn't see (a subquery the in-memory provider never translates) run on SQL Server.
/// </summary>
public class WarehousesSqlServerTests
{
    [SqlServerFact]
    public async Task ExistingStock_IsInTheDailyUseWarehouse_AndHiddenWarehousesStayOutOfListsAndHistory()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var dailyUse = await ExamReportStockTests.WithDbAsync(factory, db => db.Warehouses.Where(w => w.IsDailyUse).ToListAsync());
            dailyUse.Should().ContainSingle().Which.Name.Should().Be("Daily-use warehouse");
            var daily = dailyUse[0].Id;

            // Items as they were before warehouses: only a total.
            var (low, kept) = await ExamReportStockTests.WithDbAsync(factory, async db =>
            {
                var low = new Item("Lidocaína 2%", ItemType.Material, "SQL-LIDO", null, 10, 3000, lowStockThreshold: 5);
                var kept = new Item("Gasa estéril", ItemType.Material, "SQL-GASA", null, 3, 500, lowStockThreshold: 5);
                db.Items.AddRange(low, kept);
                await db.SaveChangesAsync();
                return (low.Id, kept.Id);
            });

            var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.ADMIN)]);
            var lockedResponse = await admin.PostAsJsonAsync("/api/inventory/warehouses", new WarehouseRequest { Name = "Locked", Roles = [Permissions.ROLES.SUPERVISOR] });
            lockedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lockedResponse.Content.ReadAsStringAsync());
            var locked = (await lockedResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
            (await admin.PostAsJsonAsync("/api/inventory/movements/transfer",
                new TransferStockRequest { ItemId = low, FromWarehouseId = daily, ToWarehouseId = locked, Quantity = 7 })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.PostAsJsonAsync("/api/inventory/warehouses", new WarehouseRequest { Name = "LOCKED" }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the unique index ignores case like the check");

            var worker = factory.CreateClientWithClaims([
                .. Permissions.ForRole(Permissions.ROLES.INVENTORY).Select(p => new Claim(Permissions.CLAIM_TYPE, p)),
                new Claim(ClaimTypes.Role, Permissions.ROLES.INVENTORY), new Claim(ClaimTypes.Name, "worker")]);

            // The worker sees 3 of the lidocaine (10 less the 7 locked away): low stock, sorted by that.
            var lowStock = (await worker.GetFromJsonAsync<PagedResponse<InventoryItemDto>>(
                $"/api/items/paged?alertFilter={StockAlertFilter.LowStock}&sortBy={ItemSortField.Stock}&sortDirection={SortDirection.Descending}"))!;
            lowStock.Items.Select(i => (i.Id, i.Stock)).Should().Equal((low, 3), (kept, 3));
            lowStock.Items.Single(i => i.Id == low).AskAt.Should().Equal("Locked");
            var all = (await admin.GetFromJsonAsync<PagedResponse<InventoryItemDto>>($"/api/items/paged?sortBy={ItemSortField.Stock}"))!;
            all.Items.Single(i => i.Id == low).Stock.Should().Be(10);

            var history = (await worker.GetFromJsonAsync<PagedResponse<MovementDto>>($"/api/inventory/movements/paged?itemId={low}"))!;
            history.Items.Should().ContainSingle().Which.Type.Should().Be(InventoryMovementType.TransferOut);
            (await worker.GetFromJsonAsync<PagedResponse<MovementDto>>($"/api/inventory/movements/paged?warehouseId={daily}"))!
                .Items.Should().OnlyContain(m => m.WarehouseId == daily);
            (await admin.GetFromJsonAsync<PagedResponse<MovementDto>>($"/api/inventory/movements/paged?itemId={low}"))!
                .TotalCount.Should().Be(2);

            (await ExamReportStockTests.WithDbAsync(factory, db => db.Items.AsNoTracking().Where(i => i.Id == low).Select(i => i.Stock).SingleAsync()))
                .Should().Be(10, "a transfer leaves the clinic's total as it was");
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
