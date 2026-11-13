using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Consumption;

/// <summary>
/// What a sheet charges, item by item. Each use is charged at the price it was recorded with. Containers used on a
/// patient are worked out from the mL given and discarded: a 10 mL ampoule with 3 + 4 mL given is one ampoule with 3 mL
/// left; 3 more mL start a second one, charged at the price of the use that started it. When the sheet is closed, what is
/// left in the open container counts as discarded.
/// </summary>
/// <param name="Balances">What is left of each item's budget, for the items the guardian approved some of.</param>
public sealed record ConsumptionSummary(List<ConsumptionLine> Lines, int Total, List<ConsumptionBalance> Balances)
{
    public static ConsumptionSummary For(ConsumptionSheet sheet)
    {
        var lines = sheet.Entries
            .GroupBy(e => e.ItemId)
            .Select(g => Line(g.OrderBy(e => e.UsedAtUtc).ThenBy(e => e.Id).ToList(), sheet.IsClosed))
            .OrderBy(l => l.ItemName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new ConsumptionSummary(lines, lines.Sum(l => l.Total), BalancesOf(sheet.Budgets, lines));
    }

    /// <summary>
    /// Each budgeted item against its uses, in the budget's unit: units or mL given, or containers started (an opened one
    /// counts whole, whatever is left in it, and whether or not it came from a quick-use box).
    /// </summary>
    private static List<ConsumptionBalance> BalancesOf(List<ConsumptionBudget> budgets, List<ConsumptionLine> lines)
        => [.. budgets
            .GroupBy(b => b.ItemId)
            .Select(g =>
            {
                var latest = g.OrderBy(b => b.ApprovedAtUtc).ThenBy(b => b.Id).Last();
                var line = lines.FirstOrDefault(l => l.ItemId == g.Key);
                var used = line is null ? 0 : latest.Charge == ConsumptionCharge.Container ? line.Containers : line.Given;
                return new ConsumptionBalance(g.Key, line?.ItemName ?? latest.ItemName, latest.Charge, g.Sum(b => b.Quantity), used, line?.LeftMl ?? 0);
            })
            .OrderBy(b => b.ItemName, StringComparer.CurrentCultureIgnoreCase)];

    private static ConsumptionLine Line(List<ConsumptionEntry> entries, bool closed)
    {
        // The latest use says how the item is charged now (shown on the sheet).
        var last = entries[^1];
        var given = entries.Where(e => !e.Discarded).Sum(e => e.Quantity);
        var discarded = entries.Where(e => e.Discarded).Sum(e => e.Quantity);

        if (last.Charge == ConsumptionCharge.Container && last.ContainerMl is > 0 and var size)
        {
            // Walks the uses in order: each one that goes past the containers started so far starts the next ones, taken
            // from stock unless the item was already out of inventory when that use was recorded.
            // When a use's unit of stock isn't the container charged (1 mL ampoules charged as one 10 mL container), it takes
            // the units it opens instead: the mL given, divided by the unit, rounded up. What is left in an open unit stays for
            // the patient, and goes with the rest of the container when that is thrown away. Each use counts with the unit it
            // was recorded with, so a use added later (after the pricing changed and the sheet was reopened) never changes
            // what the earlier ones took.
            var (containers, total, used, fromStock) = (0, 0, 0m, 0);
            var opened = new Dictionary<decimal, (int Units, decimal Ml)>();
            foreach (var entry in entries)
            {
                used += entry.Quantity;
                var needed = (int)Math.Ceiling(used / size);
                total += (needed - containers) * entry.UnitPrice;
                var stockUnit = entry.StockUnitMl is > 0 and var unit && unit != size ? unit : (decimal?)null;
                if (entry.Discarded)
                {
                    foreach (var (unitMl, open) in opened.ToList())
                        opened[unitMl] = (open.Units, open.Units * unitMl);
                }
                else if (stockUnit is decimal unitMl)
                {
                    var (units, ml) = opened.GetValueOrDefault(unitMl);
                    ml += entry.Quantity;
                    var nowOpen = (int)Math.Ceiling(ml / unitMl);
                    if (!entry.TakenOutBeforehand)
                        fromStock += Math.Max(nowOpen - units, 0);
                    opened[unitMl] = (Math.Max(units, nowOpen), ml);
                }
                if (stockUnit is null && !entry.TakenOutBeforehand)
                    fromStock += Math.Max(needed - containers, 0);
                containers = Math.Max(containers, needed);
            }
            var left = containers * size - used;
            return new ConsumptionLine(last.ItemId, last.ItemName, last.Charge, given, closed ? discarded + left : discarded,
                containers, closed ? 0 : left, size, last.UnitPrice, total, fromStock);
        }
        var sum = entries.Where(e => !e.Discarded).Sum(e => e.Charge == ConsumptionCharge.PerMl
            ? (int)Math.Round(e.Quantity * e.UnitPrice, MidpointRounding.AwayFromZero)
            : (int)Math.Round(e.Quantity, MidpointRounding.AwayFromZero) * e.UnitPrice);
        // Whole units given, except those already out of inventory; the charge per mL never takes stock (a shared bottle).
        var stockUnits = last.Charge == ConsumptionCharge.Unit
            ? (int)Math.Round(entries.Where(e => !e.Discarded && !e.TakenOutBeforehand).Sum(e => e.Quantity), MidpointRounding.AwayFromZero)
            : 0;
        return new ConsumptionLine(last.ItemId, last.ItemName, last.Charge, given, discarded, 0, 0, null, last.UnitPrice, sum, stockUnits);
    }
}

/// <summary>What is left of what the guardian approved for one item, in the budget's unit: units, containers or mL.</summary>
/// <param name="Budgeted">Approved so far: every approval adds.</param>
/// <param name="Used">Used against it: units or mL given, or containers started.</param>
/// <param name="LeftMl">mL still in the open container, for the charge by container while the sheet is open.</param>
public sealed record ConsumptionBalance(int ItemId, string ItemName, ConsumptionCharge Charge, decimal Budgeted, decimal Used, decimal LeftMl)
{
    /// <summary>Approved and not used yet; negative when more was used than approved.</summary>
    public decimal Left => Budgeted - Used;

    /// <summary>Nothing left to give, so a new budget is needed: an open container with mL in it still has some.</summary>
    public bool IsUsedUp => Left < 0 || (Left == 0 && LeftMl <= 0);

    /// <summary>What was never started, to give back at discharge: the rest of an opened container doesn't count.</summary>
    public decimal ToRefund => Math.Max(Left, 0);
}

/// <param name="Given">Units, or mL given to the patient.</param>
/// <param name="Discarded">mL thrown away from containers (with what was left at discharge once the sheet is closed).</param>
/// <param name="Containers">Containers started (charged), for the charge by container.</param>
/// <param name="LeftMl">mL still in the open container while the sheet is open.</param>
/// <param name="UnitPrice">Current price of one unit, mL or container (earlier uses keep the price they were recorded with).</param>
/// <param name="StockUnits">
/// Units of stock the line uses up: each container started (or, when a unit of stock is another size, each unit opened:
/// <see cref="ConsumptionEntry.StockUnitMl"/>), or the whole units given, leaving out the uses of items taken out of
/// inventory beforehand (<see cref="ConsumptionEntry.TakenOutBeforehand"/>). None for the charge per mL: its bottle is
/// shared by several patients and taken out by hand when it is opened.
/// </param>
public sealed record ConsumptionLine(int ItemId, string ItemName, ConsumptionCharge Charge, decimal Given, decimal Discarded,
    int Containers, decimal LeftMl, decimal? ContainerMl, int UnitPrice, int Total, int StockUnits);
