using FluentAssertions;
using VetManagement.Domain.Consumption;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Application;

/// <summary>What a consumption sheet charges at discharge, item by item.</summary>
public class ConsumptionSummaryTests
{
    private static int _minute;

    private static ConsumptionEntry Use(int itemId, ConsumptionCharge charge, decimal quantity, int price, decimal? containerMl = null, bool discarded = false)
        => new()
        {
            Id = ++_minute, ItemId = itemId, ItemName = $"Item {itemId}", Charge = charge, Quantity = quantity, UnitPrice = price,
            ContainerMl = containerMl, Discarded = discarded, UsedAtUtc = new DateTime(2026, 10, 21, 8, 0, 0).AddMinutes(_minute)
        };

    [Fact]
    public void Units_AndMl_AreChargedForWhatWasGiven()
    {
        var sheet = new ConsumptionSheet
        {
            Entries = [Use(1, ConsumptionCharge.Unit, 2, 300), Use(1, ConsumptionCharge.Unit, 1, 300),
                Use(2, ConsumptionCharge.PerMl, 1, 850), Use(2, ConsumptionCharge.PerMl, 0.5m, 850)]
        };

        var summary = ConsumptionSummary.For(sheet);

        summary.Lines.Should().ContainSingle(l => l.ItemId == 1).Which.Should().Match<ConsumptionLine>(l => l.Given == 3 && l.Total == 900);
        summary.Lines.Should().ContainSingle(l => l.ItemId == 2).Which.Should().Match<ConsumptionLine>(l => l.Given == 1.5m && l.Total == 1275);
        summary.Total.Should().Be(2175);
    }

    [Fact]
    public void Containers_AreStartedAsTheMlAddUp_AndEachOneStartedIsCharged()
    {
        var sheet = new ConsumptionSheet { Entries = [Use(5, ConsumptionCharge.Container, 3, 15000, 10), Use(5, ConsumptionCharge.Container, 4, 15000, 10)] };

        var line = ConsumptionSummary.For(sheet).Lines.Single();
        (line.Containers, line.LeftMl, line.Total).Should().Be((1, 3m, 15000));

        sheet.Entries.Add(Use(5, ConsumptionCharge.Container, 5, 15000, 10));
        line = ConsumptionSummary.For(sheet).Lines.Single();
        (line.Given, line.Containers, line.LeftMl, line.Total).Should().Be((12m, 2, 8m, 30000));
    }

    [Fact]
    public void DiscardedRest_StillCountsTowardsItsContainer_AndTheNextUseStartsANewOne()
    {
        var sheet = new ConsumptionSheet
        {
            Entries = [Use(5, ConsumptionCharge.Container, 6, 15000, 10), Use(5, ConsumptionCharge.Container, 4, 15000, 10, discarded: true),
                Use(5, ConsumptionCharge.Container, 1, 15000, 10)]
        };

        var line = ConsumptionSummary.For(sheet).Lines.Single();

        (line.Given, line.Discarded, line.Containers, line.LeftMl).Should().Be((7m, 4m, 2, 9m));
    }

    [Fact]
    public void EachContainer_IsChargedAtThePriceOfTheUseThatStartedIt()
    {
        var sheet = new ConsumptionSheet
        {
            Entries = [Use(5, ConsumptionCharge.Container, 8, 15000, 10), Use(5, ConsumptionCharge.Container, 1, 18000, 10),
                Use(5, ConsumptionCharge.Container, 13, 18000, 10)]
        };

        var line = ConsumptionSummary.For(sheet).Lines.Single();

        (line.Containers, line.Total).Should().Be((3, 15000 + 2 * 18000));
    }

    [Fact]
    public void AtDischarge_WhatIsLeftInTheOpenContainer_IsDiscarded()
    {
        var sheet = new ConsumptionSheet
        {
            ClosedAtUtc = new DateTime(2026, 10, 22),
            Entries = [Use(5, ConsumptionCharge.Container, 2.5m, 15000, 10)]
        };

        var line = ConsumptionSummary.For(sheet).Lines.Single();

        (line.Containers, line.LeftMl, line.Discarded, line.Total).Should().Be((1, 0m, 7.5m, 15000));
    }
}
