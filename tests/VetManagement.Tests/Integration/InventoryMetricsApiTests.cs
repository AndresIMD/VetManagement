using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Inventory;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>The inventory metrics add up the recorded movements by clinic-local dates, on the server.</summary>
public class InventoryMetricsApiTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));
    private static DateTime LocalUtc(DateOnly date, int hour) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, 0)), Zone);

    private static InventoryMovement Move(Item item, InventoryMovementType type, int quantity, DateOnly date, int hour = 10)
        => new() { ItemId = item.Id, Type = type, Quantity = quantity, Date = LocalUtc(date, hour), Responsible = "test" };

    private static async Task<(Item Gauze, Item Syringe, Item Collar)> SeedAsync(CustomWebAppFactory app)
    {
        var today = Today;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gauze = new Item("Gauze", ItemType.Material, "MET-1", null, stock: 3, sellPrice: 1000, lowStockThreshold: 5, buyPrice: 400);
        var syringe = new Item("Syringe", ItemType.Material, "MET-2", null, stock: 100, sellPrice: 500, buyPrice: 100);
        var collar = new Item("Collar", ItemType.Material, "MET-3", null, stock: 0, sellPrice: 3000);
        db.Items.AddRange(gauze, syringe, collar);
        await db.SaveChangesAsync();

        db.InventoryMovements.AddRange(
            Move(gauze, InventoryMovementType.Egress, 6, today.AddDays(-1)),
            Move(gauze, InventoryMovementType.Ingress, 10, today.AddDays(-2)),
            Move(syringe, InventoryMovementType.MassiveStockEgress, 4, today.AddDays(-1)),
            Move(syringe, InventoryMovementType.Egress, 2, today.AddDays(-1)),
            Move(collar, InventoryMovementType.Egress, 1, today.AddDays(-3)),
            // The day before the period, late in the evening: already the next day in UTC, but not in the clinic.
            Move(gauze, InventoryMovementType.Egress, 1, today.AddDays(-7), hour: 22),
            Move(gauze, InventoryMovementType.Egress, 4, today.AddDays(-10)));
        await db.SaveChangesAsync();
        return (gauze, syringe, collar);
    }

    [Fact]
    public async Task Metrics_AddUpTheFlowOfThePeriod_ByLocalDate()
    {
        await using var app = new CustomWebAppFactory(); // its own database, so the totals are exact
        var (gauze, _, collar) = await SeedAsync(app);
        var manager = app.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR])]);
        var today = Today;

        var m = (await manager.GetFromJsonAsync<InventoryMetricsDto>($"/api/inventory/metrics?from={today.AddDays(-6):yyyy-MM-dd}&to={today:yyyy-MM-dd}"))!;

        m.Grouping.Should().Be(MetricsGrouping.Day);
        m.UnitsOut.Should().Be(13);
        m.UnitsIn.Should().Be(10);
        m.PreviousUnitsOut.Should().Be(5, "the late movement belongs to the clinic's previous day");
        m.Movements.Should().Be(5);
        m.ItemsMoved.Should().Be(3);
        m.SaleValueOut.Should().Be(6 * 1000 + 6 * 500 + 1 * 3000);
        m.CostValueOut.Should().Be(6 * 400 + 6 * 100);
        m.OutOfStock.Should().Be(1);
        m.LowStock.Should().Be(1);

        m.Buckets.Should().HaveCount(7);
        m.Buckets.Single(b => DateOnly.FromDateTime(b.Start) == today.AddDays(-1)).UnitsOut.Should().Be(12);
        m.Buckets.Single(b => DateOnly.FromDateTime(b.Start) == today.AddDays(-2)).UnitsIn.Should().Be(10);

        m.TopItems.Select(i => i.Name).Should().Equal("Gauze", "Syringe", "Collar");
        m.TopItems[0].DaysOfCover.Should().Be(3.5, "3 in stock at 6 every 7 days");
        m.RunningOut.Select(i => i.ItemId).Should().Equal([collar.Id, gauze.Id], "the syringes last months");
        m.Series.Should().BeEmpty();
    }

    [Fact]
    public async Task ChosenItems_NarrowTheFigures_AndEachGetsItsLines_ButTheMostUsedStayTheInventorys()
    {
        await using var app = new CustomWebAppFactory();
        var (gauze, _, _) = await SeedAsync(app);
        var manager = app.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR])]);
        var today = Today;

        var m = (await manager.GetFromJsonAsync<InventoryMetricsDto>(
            $"/api/inventory/metrics?from={today.AddDays(-6):yyyy-MM-dd}&to={today:yyyy-MM-dd}&itemIds={gauze.Id}"))!;

        m.UnitsOut.Should().Be(6);
        m.ItemsMoved.Should().Be(1);
        m.RunningOut.Should().ContainSingle().Which.ItemId.Should().Be(gauze.Id);
        var line = m.Series.Should().ContainSingle().Subject;
        line.UnitsOut.Sum().Should().Be(6);
        line.UnitsIn.Sum().Should().Be(10);
        line.UnitsIn[^3].Should().Be(10, "it came in two days ago");
        m.TopItems.Select(i => i.Name).Should().Equal(["Gauze", "Syringe", "Collar"], "more items are picked from this list");
    }

    [Fact]
    public async Task Metrics_AreForSupervisors_AndLimitTheRangeAndTheItems()
    {
        await using var app = new CustomWebAppFactory();
        var today = Today;
        var range = $"from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}";
        var manager = app.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR])]);

        (await app.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.INVENTORY)]).GetAsync($"/api/inventory/metrics?{range}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync($"/api/inventory/metrics?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await manager.GetAsync($"/api/inventory/metrics?from={today.AddDays(-400):yyyy-MM-dd}&to={today:yyyy-MM-dd}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var tooMany = string.Concat(Enumerable.Range(1, InventoryMetricsService.MaxCompared + 1).Select(i => $"&itemIds={i}"));
        (await manager.GetAsync($"/api/inventory/metrics?{range}{tooMany}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var day = (await manager.GetFromJsonAsync<InventoryMetricsDto>($"/api/inventory/metrics?{range}"))!;
        day.Grouping.Should().Be(MetricsGrouping.Hour);
        day.Buckets.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(24);
    }

    [Theory]
    [InlineData(1, MetricsGrouping.Hour)]
    [InlineData(30, MetricsGrouping.Day)]
    [InlineData(90, MetricsGrouping.Week)]
    [InlineData(365, MetricsGrouping.Month)]
    public void LongerRanges_UseWiderSlices(int days, MetricsGrouping expected)
        => InventoryMetricsService.GroupingFor(Today.AddDays(1 - days), Today).Should().Be(expected);
}
