using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Consumption;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>Uses recorded before the quick-use box mark existed keep taking stock as they did once the database is updated.</summary>
public class ConsumptionSqlServerTests
{
    /// <summary>The last migration before the quick-use box mark.</summary>
    private const string BEFORE_THE_BOX = "20261102231739_ConsumptionTakesStock";

    [SqlServerFact]
    public async Task ASheetRecordedBeforeTheQuickUseBox_KeepsWhatItTook_AfterTheUpdate()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>().MigrateAsync(BEFORE_THE_BOX);
        try
        {
            // An open sheet as the previous version left it: 2 gauzes used and taken (10 → 8). Written in SQL, since the
            // current model has columns that database doesn't have yet.
            var item = await ExamReportStockTests.WithDbAsync(factory, async db =>
            {
                var gauze = new Item("Gasa estéril", ItemType.Material, "SQL-GASA-BOX", null, 8, 500);
                db.Items.Add(gauze);
                await db.SaveChangesAsync();
                return gauze.Id;
            });
            var sheet = await ExamReportStockTests.WithDbAsync(factory, async db => (await db.Database.SqlQuery<int>($"""
                INSERT INTO ConsumptionSheets (PatientName, OpenedAtUtc, MovesStock, StockPending)
                OUTPUT INSERTED.Id AS Value VALUES ('TOBY', {DateTime.UtcNow}, 1, 0)
                """).ToListAsync()).Single());
            await ExamReportStockTests.WithDbAsync(factory, db => db.Database.ExecuteSqlAsync($"""
                INSERT INTO ConsumptionEntries (SheetId, ItemId, ItemName, Charge, UnitPrice, Quantity, Discarded, UsedAtUtc)
                VALUES ({sheet}, {item}, 'Gasa estéril', 0, 500, 2, 0, {DateTime.UtcNow});
                INSERT INTO ConsumptionStock (SheetId, ItemId, Units) VALUES ({sheet}, {item}, 2);
                """));

            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

            // The next use makes the sheet match its uses again: the earlier ones still count as taken, nothing comes back.
            var lab = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.LABORATORY, Permissions.ROLES.INVENTORY])]);
            (await lab.PostAsJsonAsync($"/api/consumption-sheets/{sheet}/entries", new AddConsumptionEntryRequest { ItemId = item, Quantity = 1 }))
                .EnsureSuccessStatusCode();

            (await ExamReportStockTests.WithDbAsync(factory, db => db.Items.AsNoTracking().Where(i => i.Id == item).Select(i => i.Stock).SingleAsync()))
                .Should().Be(7);
            (await ExamReportStockTests.WithDbAsync(factory, db => db.ConsumptionStock.AsNoTracking().Where(s => s.SheetId == sheet).Select(s => s.Units).SingleAsync()))
                .Should().Be(3);
            (await ExamReportStockTests.WithDbAsync(factory, db => db.InventoryMovements.AsNoTracking().Where(m => m.ItemId == item).ToListAsync()))
                .Should().ContainSingle().Which.Should().Match<InventoryMovement>(m => m.Type == InventoryMovementType.Egress && m.Quantity == 1);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
