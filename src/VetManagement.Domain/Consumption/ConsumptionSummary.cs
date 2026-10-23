using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Consumption;

/// <summary>
/// What a sheet charges, item by item. Each use is charged at the price it was recorded with. Containers used on a
/// patient are worked out from the mL given and discarded: a 10 mL ampoule with 3 + 4 mL given is one ampoule with 3 mL
/// left; 3 more mL start a second one, charged at the price of the use that started it. When the sheet is closed, what is
/// left in the open container counts as discarded.
/// </summary>
public sealed record ConsumptionSummary(List<ConsumptionLine> Lines, int Total)
{
    public static ConsumptionSummary For(ConsumptionSheet sheet)
    {
        var lines = sheet.Entries
            .GroupBy(e => e.ItemId)
            .Select(g => Line(g.OrderBy(e => e.UsedAtUtc).ThenBy(e => e.Id).ToList(), sheet.IsClosed))
            .OrderBy(l => l.ItemName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new ConsumptionSummary(lines, lines.Sum(l => l.Total));
    }

    private static ConsumptionLine Line(List<ConsumptionEntry> entries, bool closed)
    {
        // The latest use says how the item is charged now (shown on the sheet).
        var last = entries[^1];
        var given = entries.Where(e => !e.Discarded).Sum(e => e.Quantity);
        var discarded = entries.Where(e => e.Discarded).Sum(e => e.Quantity);

        if (last.Charge == ConsumptionCharge.Container && last.ContainerMl is > 0 and var size)
        {
            // Walks the uses in order: each one that goes past the containers started so far starts the next ones.
            var (containers, total, used) = (0, 0, 0m);
            foreach (var entry in entries)
            {
                used += entry.Quantity;
                var needed = (int)Math.Ceiling(used / size);
                total += (needed - containers) * entry.UnitPrice;
                containers = Math.Max(containers, needed);
            }
            var left = containers * size - used;
            return new ConsumptionLine(last.ItemId, last.ItemName, last.Charge, given, closed ? discarded + left : discarded,
                containers, closed ? 0 : left, size, last.UnitPrice, total);
        }
        var sum = entries.Where(e => !e.Discarded).Sum(e => e.Charge == ConsumptionCharge.PerMl
            ? (int)Math.Round(e.Quantity * e.UnitPrice, MidpointRounding.AwayFromZero)
            : (int)Math.Round(e.Quantity, MidpointRounding.AwayFromZero) * e.UnitPrice);
        return new ConsumptionLine(last.ItemId, last.ItemName, last.Charge, given, discarded, 0, 0, null, last.UnitPrice, sum);
    }
}

/// <param name="Given">Units, or mL given to the patient.</param>
/// <param name="Discarded">mL thrown away from containers (with what was left at discharge once the sheet is closed).</param>
/// <param name="Containers">Containers started (charged), for the charge by container.</param>
/// <param name="LeftMl">mL still in the open container while the sheet is open.</param>
/// <param name="UnitPrice">Current price of one unit, mL or container (earlier uses keep the price they were recorded with).</param>
public sealed record ConsumptionLine(int ItemId, string ItemName, ConsumptionCharge Charge, decimal Given, decimal Discarded,
    int Containers, decimal LeftMl, decimal? ContainerMl, int UnitPrice, int Total);
