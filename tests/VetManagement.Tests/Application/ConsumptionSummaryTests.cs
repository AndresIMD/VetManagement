using FluentAssertions;
using VetManagement.Domain.Consumption;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Application;

/// <summary>What a consumption sheet charges at discharge, item by item.</summary>
public class ConsumptionSummaryTests
{
    private static int _minute;

    private static ConsumptionEntry Use(int itemId, ConsumptionCharge charge, decimal quantity, int price, decimal? containerMl = null, bool discarded = false,
        bool beforehand = false)
        => new()
        {
            Id = ++_minute, ItemId = itemId, ItemName = $"Item {itemId}", Charge = charge, Quantity = quantity, UnitPrice = price,
            ContainerMl = containerMl, Discarded = discarded, TakenOutBeforehand = beforehand,
            UsedAtUtc = new DateTime(2026, 10, 21, 8, 0, 0).AddMinutes(_minute)
        };

    private static ConsumptionBudget Budget(int itemId, ConsumptionCharge charge, decimal quantity)
        => new()
        {
            Id = ++_minute, ItemId = itemId, ItemName = $"Item {itemId}", Charge = charge, Quantity = quantity,
            ApprovedAtUtc = new DateTime(2026, 10, 21, 7, 0, 0).AddMinutes(_minute)
        };

    [Fact]
    public void Budgets_CountDownInTheirUnit_AndOnlyWhatWasNeverStartedIsToRefund()
    {
        var sheet = new ConsumptionSheet
        {
            Budgets = [Budget(1, ConsumptionCharge.Unit, 5), Budget(2, ConsumptionCharge.Container, 2), Budget(3, ConsumptionCharge.PerMl, 10),
                Budget(4, ConsumptionCharge.Unit, 3), Budget(1, ConsumptionCharge.Unit, 2)],
            Entries = [Use(1, ConsumptionCharge.Unit, 2, 300), Use(2, ConsumptionCharge.Container, 12, 15000, 10), Use(3, ConsumptionCharge.PerMl, 2.5m, 850)]
        };

        var summary = ConsumptionSummary.For(sheet);
        var balances = summary.Balances.ToDictionary(b => b.ItemId);

        balances[1].Should().Match<ConsumptionBalance>(b => b.Budgeted == 7 && b.Used == 2 && b.Left == 5 && !b.IsUsedUp && b.ToRefund == 5,
            "every approval adds");
        balances[2].Should().Match<ConsumptionBalance>(b => b.Used == 2 && b.Left == 0 && b.LeftMl == 8 && !b.IsUsedUp && b.ToRefund == 0,
            "two containers started; the open one still has 8 mL for the patient, and isn't given back");
        balances[3].Should().Match<ConsumptionBalance>(b => b.Used == 2.5m && b.Left == 7.5m);
        balances[4].Should().Match<ConsumptionBalance>(b => b.Used == 0 && b.Left == 3, "approved, not used yet");
        summary.Lines.Select(l => l.ItemId).Should().Equal([1, 2, 3], "a budget alone charges nothing");
    }

    [Fact]
    public void ABudget_IsUsedUp_WhenNothingIsLeftToGive_OrMoreWasUsed()
    {
        var sheet = new ConsumptionSheet
        {
            Budgets = [Budget(1, ConsumptionCharge.Unit, 2), Budget(2, ConsumptionCharge.Container, 1)],
            Entries = [Use(1, ConsumptionCharge.Unit, 3, 300), Use(2, ConsumptionCharge.Container, 10, 15000, 10)]
        };

        var balances = ConsumptionSummary.For(sheet).Balances.ToDictionary(b => b.ItemId);

        balances[1].Should().Match<ConsumptionBalance>(b => b.Left == -1 && b.IsUsedUp && b.ToRefund == 0);
        balances[2].Should().Match<ConsumptionBalance>(b => b.Left == 0 && b.LeftMl == 0 && b.IsUsedUp, "the only container is finished");
    }

    [Fact]
    public void UsesOfItemsTakenOutBeforehand_AreChargedButTakeNoStock()
    {
        var sheet = new ConsumptionSheet
        {
            Entries = [Use(1, ConsumptionCharge.Unit, 2, 300, beforehand: true), Use(2, ConsumptionCharge.Container, 3, 15000, 10, beforehand: true),
                Use(3, ConsumptionCharge.Unit, 2, 300), Use(4, ConsumptionCharge.PerMl, 1, 850)]
        };

        var lines = ConsumptionSummary.For(sheet).Lines.ToDictionary(l => l.ItemId);

        lines[1].Should().Match<ConsumptionLine>(l => l.Total == 600 && l.StockUnits == 0, "the quick-use box was already taken out");
        lines[2].Should().Match<ConsumptionLine>(l => l.Containers == 1 && l.Total == 15000 && l.StockUnits == 0);
        lines[3].StockUnits.Should().Be(2, "whole units given are taken");
        lines[4].StockUnits.Should().Be(0, "the shared bottle is taken out by hand");
    }

    [Fact]
    public void WhenTheBoxSettingChangesMidStay_EachUseKeepsWhatItWasRecordedWith()
    {
        // Two 10 mL containers from stock, then the item comes from the box: the third container takes nothing.
        var sheet = new ConsumptionSheet
        {
            Entries = [Use(5, ConsumptionCharge.Container, 8, 15000, 10), Use(5, ConsumptionCharge.Container, 8, 15000, 10),
                Use(5, ConsumptionCharge.Container, 8, 15000, 10, beforehand: true)]
        };

        var line = ConsumptionSummary.For(sheet).Lines.Single();

        line.Containers.Should().Be(3);
        line.StockUnits.Should().Be(2);
    }

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
