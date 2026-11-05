using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Inventory;
using VetManagement.Application.Services;
using VetManagement.Domain.Consumption;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Localization;

namespace VetManagement.Application.Consumption;

public enum ConsumptionStatus
{
    Done,
    NotFound,
    Invalid
}

public sealed record ConsumptionResult(ConsumptionStatus Status, int? Id = null, string? Error = null);

public sealed record ConsumptionPricingView(ConsumptionPricing Pricing, Item Item);

/// <summary>
/// Consumption sheets: the supplies used on a patient during a stay, charged at discharge. Each use copies how its item is
/// charged (<see cref="ConsumptionPricing"/>, or per unit at the sale price); the sheet adds them up
/// (<see cref="ConsumptionSummary"/>). Sheets that move stock take each use from the daily-use warehouse as it's recorded
/// (<see cref="ConsumptionLine.StockUnits"/>), under the clinic-wide stock lock; a use is never refused for want of stock.
/// </summary>
public class ConsumptionService(IUnitOfWork unitOfWork, AuditService audit, TimeProvider clock,
    IRealtimeNotificationService? notifications = null)
{
    public const decimal MaxQuantity = 10000;

    public async Task<List<ConsumptionSheet>> ListAsync(int closedInLastDays)
        => await unitOfWork.ConsumptionSheets.ListAsync(closedInLastDays > 0 ? clock.GetUtcNow().UtcDateTime.AddDays(-closedInLastDays) : null);

    public async Task<ConsumptionSheet?> GetAsync(int id) => await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(id);

    /// <summary>A sheet for a registered pet (its name and owner are copied) or for a patient typed in.</summary>
    public async Task<ConsumptionResult> OpenAsync(int? petId, string? patientName, string? ownerName, string? notes, string userName)
    {
        var sheet = new ConsumptionSheet { Notes = Clean(notes), OpenedAtUtc = clock.GetUtcNow().UtcDateTime, OpenedBy = userName, MovesStock = true };
        if (petId is int id)
        {
            var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(id);
            if (pet is null)
                return Invalid(Texts.T("The patient doesn't exist."));
            var owner = await unitOfWork.Clients.GetByIdAsNoTrackingAsync(pet.OwnerId);
            sheet.PetId = pet.Id;
            sheet.PatientName = pet.Name;
            sheet.OwnerName = owner is null ? null : Clean($"{owner.Name} {owner.LastName}");
        }
        else if (!string.IsNullOrWhiteSpace(patientName))
        {
            sheet.PatientName = Clean(patientName)!;
            sheet.OwnerName = Clean(ownerName);
        }
        else
            return Invalid(Texts.T("Choose a registered patient or type the patient's name."));

        await unitOfWork.ConsumptionSheets.AddAsync(sheet);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheet.Id, AuditActionType.Add, $"{sheet.PatientName} · {sheet.OwnerName}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
    }

    /// <summary>
    /// Records a use: units, or mL for items charged by mL or by container. On a sheet that moves stock it takes what the use
    /// needs from the daily-use warehouse; when that has too few, the use is still recorded and the rest stays pending.
    /// </summary>
    public async Task<ConsumptionResult> AddEntryAsync(int sheetId, int itemId, decimal quantity, string? notes, string userName)
    {
        string? recorded = null;
        List<int> moved = [];
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
            if (sheet is null)
                return new ConsumptionResult(ConsumptionStatus.NotFound);
            if (sheet.IsClosed)
                return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));
            var item = await unitOfWork.Items.GetByIdAsNoTrackingAsync(itemId);
            if (item is null)
                return Invalid(Texts.T("The item doesn't exist."));
            var pricing = (await unitOfWork.ConsumptionPricings.FindAsync(p => p.ItemId == itemId)).FirstOrDefault();
            var charge = pricing?.Charge ?? ConsumptionCharge.Unit;
            if (quantity <= 0 || quantity > MaxQuantity)
                return Invalid(Texts.T("The quantity must be more than 0 and at most {0}.", MaxQuantity));
            if (charge == ConsumptionCharge.Unit && quantity != decimal.Truncate(quantity))
                return Invalid(Texts.T("'{0}' is charged per unit: use whole numbers.", item.Name));

            var now = clock.GetUtcNow().UtcDateTime;
            var entry = new ConsumptionEntry
            {
                SheetId = sheet.Id,
                ItemId = item.Id,
                ItemName = item.Name,
                Charge = charge,
                ContainerMl = charge == ConsumptionCharge.Container ? pricing!.ContainerMl : null,
                UnitPrice = charge == ConsumptionCharge.PerMl ? pricing!.PricePerMl ?? 0 : item.SellPrice,
                TakenOutBeforehand = pricing?.TakenOutBeforehand ?? false,
                Quantity = Math.Round(quantity, 2),
                Notes = Clean(notes),
                UsedAtUtc = now,
                CreatedBy = userName
            };
            sheet.Entries.Add(entry);
            moved = await TakeStockAsync(sheet, userName, now);
            await unitOfWork.SaveChangesAsync();
            recorded = $"+ {entry.ItemName}: {entry.Quantity}";
            return new ConsumptionResult(ConsumptionStatus.Done, entry.Id);
        });

        if (result.Status == ConsumptionStatus.Done)
        {
            await audit.LogAsync(nameof(ConsumptionSheet), sheetId, AuditActionType.Edit, recorded!, userName);
            await Stock.NotifyChangedAsync(notifications, moved);
        }
        return result;
    }

    /// <summary>
    /// Throws away what is left in the patient's open container of that item; the next use starts a new one. The container was
    /// taken from stock when it was started, so nothing moves for the rest thrown away.
    /// </summary>
    public async Task<ConsumptionResult> DiscardRestAsync(int sheetId, int itemId, string userName)
    {
        string? discarded = null;
        List<int> moved = [];
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
            if (sheet is null)
                return new ConsumptionResult(ConsumptionStatus.NotFound);
            if (sheet.IsClosed)
                return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));
            var line = ConsumptionSummary.For(sheet).Lines.FirstOrDefault(l => l.ItemId == itemId);
            if (line is not { Charge: ConsumptionCharge.Container, LeftMl: > 0 })
                return Invalid(Texts.T("There is no open container of this item."));

            var now = clock.GetUtcNow().UtcDateTime;
            var last = sheet.Entries.Where(e => e.ItemId == itemId).MaxBy(e => e.UsedAtUtc)!;
            sheet.Entries.Add(new ConsumptionEntry
            {
                SheetId = sheet.Id, ItemId = itemId, ItemName = last.ItemName, Charge = last.Charge, ContainerMl = last.ContainerMl,
                UnitPrice = last.UnitPrice, TakenOutBeforehand = last.TakenOutBeforehand, Quantity = line.LeftMl, Discarded = true,
                UsedAtUtc = now, CreatedBy = userName
            });
            moved = await TakeStockAsync(sheet, userName, now);
            await unitOfWork.SaveChangesAsync();
            discarded = $"Discarded {line.ItemName}: {line.LeftMl}";
            return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
        });

        if (result.Status == ConsumptionStatus.Done)
        {
            await audit.LogAsync(nameof(ConsumptionSheet), sheetId, AuditActionType.Edit, discarded!, userName);
            await Stock.NotifyChangedAsync(notifications, moved);
        }
        return result;
    }

    /// <summary>
    /// Removes a use recorded by mistake, while the sheet is open. What the sheet took for it goes back to the daily-use
    /// warehouse, unless the remaining uses still need it (a container another use also drew from).
    /// </summary>
    public async Task<ConsumptionResult> RemoveEntryAsync(int sheetId, int entryId, string userName)
    {
        string? removed = null;
        List<int> moved = [];
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
            var entry = sheet?.Entries.FirstOrDefault(e => e.Id == entryId);
            if (sheet is null || entry is null)
                return new ConsumptionResult(ConsumptionStatus.NotFound);
            if (sheet.IsClosed)
                return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));

            sheet.Entries.Remove(entry);
            moved = await TakeStockAsync(sheet, userName, clock.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync();
            removed = $"- {entry.ItemName}: {entry.Quantity}{(entry.Discarded ? " (discarded)" : "")} ({entry.CreatedBy}, {entry.UsedAtUtc:u})";
            return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
        });

        if (result.Status == ConsumptionStatus.Done)
        {
            await audit.LogAsync(nameof(ConsumptionSheet), sheetId, AuditActionType.Edit, removed!, userName);
            await Stock.NotifyChangedAsync(notifications, moved);
        }
        return result;
    }

    /// <summary>
    /// Adds what the guardian approved of an item (e.g. 5 ampoules), in its charge unit: the uses count down from it, and each
    /// approval adds. Only the quantity: the money stays in the clinic's billing system. It moves no stock.
    /// </summary>
    public async Task<ConsumptionResult> AddBudgetAsync(int sheetId, int itemId, decimal quantity, string? notes, string userName)
    {
        var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
        if (sheet is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        if (sheet.IsClosed)
            return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));
        var item = await unitOfWork.Items.GetByIdAsNoTrackingAsync(itemId);
        if (item is null)
            return Invalid(Texts.T("The item doesn't exist."));
        var charge = (await unitOfWork.ConsumptionPricings.FindAsync(p => p.ItemId == itemId)).FirstOrDefault()?.Charge ?? ConsumptionCharge.Unit;
        if (quantity <= 0 || quantity > MaxQuantity)
            return Invalid(Texts.T("The quantity must be more than 0 and at most {0}.", MaxQuantity));
        if (charge != ConsumptionCharge.PerMl && quantity != decimal.Truncate(quantity))
            return Invalid(Texts.T("'{0}' is budgeted in whole units or containers.", item.Name));

        var budget = new ConsumptionBudget
        {
            SheetId = sheet.Id,
            ItemId = item.Id,
            ItemName = item.Name,
            Charge = charge,
            Quantity = Math.Round(quantity, 2),
            Notes = Clean(notes),
            ApprovedAtUtc = clock.GetUtcNow().UtcDateTime,
            CreatedBy = userName
        };
        sheet.Budgets.Add(budget);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheetId, AuditActionType.Edit, $"Budget + {budget.ItemName}: {budget.Quantity}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, budget.Id);
    }

    /// <summary>Removes an approval recorded by mistake.</summary>
    public async Task<ConsumptionResult> RemoveBudgetAsync(int sheetId, int budgetId, string userName)
    {
        var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
        var budget = sheet?.Budgets.FirstOrDefault(b => b.Id == budgetId);
        if (sheet is null || budget is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        if (sheet.IsClosed)
            return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));

        sheet.Budgets.Remove(budget);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheetId, AuditActionType.Edit,
            $"Budget - {budget.ItemName}: {budget.Quantity} ({budget.CreatedBy}, {budget.ApprovedAtUtc:u})", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
    }

    /// <summary>Discharge: the sheet takes no more entries and what is left in open containers counts as discarded.</summary>
    public async Task<ConsumptionResult> CloseAsync(int id, string userName)
        => await SetClosedAsync(id, closed: true, userName);

    public async Task<ConsumptionResult> ReopenAsync(int id, string userName)
        => await SetClosedAsync(id, closed: false, userName);

    /// <summary>Deletes the sheet; what it took from inventory goes back to the daily-use warehouse.</summary>
    public async Task<ConsumptionResult> DeleteAsync(int id, string userName)
    {
        string? snapshot = null;
        List<int> moved = [];
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(id);
            if (sheet is null)
                return new ConsumptionResult(ConsumptionStatus.NotFound);
            snapshot = JsonSerializer.Serialize(sheet);
            moved = [];
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var taken in sheet.Stock.Where(s => s.Units > 0))
                if (await Stock.MoveAsync(unitOfWork, taken.ItemId, taken.Units, InventoryMovementType.Ingress,
                        StockReason(sheet, "Consumption sheet #{0} – {1} (deleted)"), userName, now) is not null)
                    moved.Add(taken.ItemId);
            unitOfWork.ConsumptionSheets.Remove(sheet);
            await unitOfWork.SaveChangesAsync();
            return new ConsumptionResult(ConsumptionStatus.Done, id);
        });

        if (result.Status == ConsumptionStatus.Done)
        {
            await audit.LogAsync(nameof(ConsumptionSheet), id, AuditActionType.Delete, snapshot!, userName);
            await Stock.NotifyChangedAsync(notifications, moved);
        }
        return result;
    }

    public async Task<List<ConsumptionPricingView>> ListPricingsAsync()
    {
        var pricings = await unitOfWork.ConsumptionPricings.GetAllAsync();
        var itemIds = pricings.Select(p => p.ItemId).ToList();
        var items = (await unitOfWork.Items.FindAsync(i => itemIds.Contains(i.Id))).ToDictionary(i => i.Id);
        return [.. pricings.Where(p => items.ContainsKey(p.ItemId))
            .Select(p => new ConsumptionPricingView(p, items[p.ItemId]))
            .OrderBy(v => v.Item.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Sets how an item is charged on the sheets; uses already recorded keep the charge they were recorded with.</summary>
    /// <param name="takenOutBeforehand">The item leaves inventory when a quick-use box is restocked; ignored when charged per mL.</param>
    public async Task<ConsumptionResult> SetPricingAsync(int itemId, ConsumptionCharge charge, decimal? containerMl, int? pricePerMl, bool controlled,
        bool takenOutBeforehand, string userName)
    {
        var item = await unitOfWork.Items.GetByIdAsNoTrackingAsync(itemId);
        if (item is null)
            return Invalid(Texts.T("The item doesn't exist."));
        if (!Enum.IsDefined(charge))
            return Invalid(Texts.T("Invalid request."));
        if (charge == ConsumptionCharge.Container && containerMl is not (> 0 and <= MaxQuantity))
            return Invalid(Texts.T("Write how many mL one container holds."));
        if (charge == ConsumptionCharge.PerMl && pricePerMl is not >= 0)
            return Invalid(Texts.T("Write the price of one mL."));

        var pricing = (await unitOfWork.ConsumptionPricings.FindAsync(p => p.ItemId == itemId)).FirstOrDefault();
        var size = charge == ConsumptionCharge.Container ? Math.Round(containerMl!.Value, 2) : (decimal?)null;
        var beforehand = takenOutBeforehand && charge != ConsumptionCharge.PerMl;
        if (ChangesHowUsesAddUp(pricing, charge, size, beforehand) && await unitOfWork.ConsumptionSheets.IsOnOpenSheetAsync(itemId))
            return Invalid(Texts.T("'{0}' is on sheets not discharged yet: change how it's charged once they are.", item.Name));
        var isNew = pricing is null;
        pricing ??= new ConsumptionPricing { ItemId = itemId };
        pricing.Charge = charge;
        pricing.ContainerMl = size;
        pricing.PricePerMl = charge == ConsumptionCharge.PerMl ? pricePerMl : null;
        pricing.Controlled = controlled;
        pricing.TakenOutBeforehand = beforehand;
        pricing.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        pricing.UpdatedBy = userName;
        if (isNew)
            await unitOfWork.ConsumptionPricings.AddAsync(pricing);
        else
            unitOfWork.ConsumptionPricings.Update(pricing);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionPricing), pricing.Id, isNew ? AuditActionType.Add : AuditActionType.Edit,
            $"{item.Name}: {charge}, {pricing.ContainerMl} mL, {pricing.PricePerMl}/mL, controlled {controlled}, taken out beforehand {beforehand}",
            userName);
        return new ConsumptionResult(ConsumptionStatus.Done, pricing.Id);
    }

    /// <summary>The item goes back to being charged per unit at its sale price.</summary>
    public async Task<ConsumptionResult> DeletePricingAsync(int itemId, string userName)
    {
        var pricing = (await unitOfWork.ConsumptionPricings.FindAsync(p => p.ItemId == itemId)).FirstOrDefault();
        if (pricing is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        if (ChangesHowUsesAddUp(pricing, ConsumptionCharge.Unit, null, false) && await unitOfWork.ConsumptionSheets.IsOnOpenSheetAsync(itemId))
            return Invalid(Texts.T("'{0}' is on sheets not discharged yet: change how it's charged once they are.",
                (await unitOfWork.Items.GetByIdAsNoTrackingAsync(itemId))?.Name ?? string.Empty));
        unitOfWork.ConsumptionPricings.Remove(pricing);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionPricing), pricing.Id, AuditActionType.Delete, $"Item {itemId}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, pricing.Id);
    }

    /// <summary>Closes or reopens the sheet; either way it takes now what the daily-use warehouse couldn't cover before.</summary>
    private async Task<ConsumptionResult> SetClosedAsync(int id, bool closed, string userName)
    {
        var changed = false;
        List<int> moved = [];
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(id);
            if (sheet is null)
                return new ConsumptionResult(ConsumptionStatus.NotFound);
            changed = sheet.IsClosed != closed;
            if (!changed)
                return new ConsumptionResult(ConsumptionStatus.Done, id);
            var now = clock.GetUtcNow().UtcDateTime;
            sheet.ClosedAtUtc = closed ? now : null;
            sheet.ClosedBy = closed ? userName : null;
            moved = await TakeStockAsync(sheet, userName, now);
            await unitOfWork.SaveChangesAsync();
            return new ConsumptionResult(ConsumptionStatus.Done, id);
        });

        if (result.Status == ConsumptionStatus.Done && changed)
        {
            await audit.LogAsync(nameof(ConsumptionSheet), id, AuditActionType.Edit, closed ? "Closed (discharge)" : "Reopened", userName);
            await Stock.NotifyChangedAsync(notifications, moved);
        }
        return result;
    }

    /// <summary>
    /// Brings what the sheet has taken from the daily-use warehouse in line with what its uses need
    /// (<see cref="ConsumptionLine.StockUnits"/>): takes what is missing, as far as the warehouse has it, and puts back what a
    /// removed use no longer needs. It never refuses: what the warehouse can't cover stays pending, and every later change of
    /// the sheet, discharge included, tries again. The caller saves. Returns the items whose stock moved.
    /// </summary>
    private async Task<List<int>> TakeStockAsync(ConsumptionSheet sheet, string userName, DateTime nowUtc)
    {
        if (!sheet.MovesStock)
            return [];
        var needed = ConsumptionSummary.For(sheet).Lines.ToDictionary(l => l.ItemId, l => l.StockUnits);
        var moved = new List<int>();
        foreach (var itemId in needed.Keys.Union(sheet.Stock.Select(s => s.ItemId)).ToList())
        {
            var target = needed.GetValueOrDefault(itemId);
            var row = sheet.Stock.FirstOrDefault(s => s.ItemId == itemId);
            var taken = row?.Units ?? 0;
            if (target == taken)
                continue;

            int units;
            var item = await unitOfWork.Items.GetByIdAsync(itemId);
            if (item is null)
                units = target; // deleted from the inventory: nothing to move, and nothing left pending
            else if (target > taken)
            {
                var take = Math.Min(target - taken, Math.Max(await Stock.InDailyUseAsync(unitOfWork, item), 0));
                if (take > 0)
                {
                    await Stock.MoveAsync(unitOfWork, itemId, take, InventoryMovementType.Egress,
                        StockReason(sheet, "Consumption sheet #{0} – {1}"), userName, nowUtc);
                    moved.Add(itemId);
                }
                units = taken + take;
            }
            else
            {
                await Stock.MoveAsync(unitOfWork, itemId, taken - target, InventoryMovementType.Ingress,
                    StockReason(sheet, "Consumption sheet #{0} – {1} (returned)"), userName, nowUtc);
                moved.Add(itemId);
                units = target;
            }

            if (row is not null)
                row.Units = units;
            else if (units > 0)
                sheet.Stock.Add(new ConsumptionStock { SheetId = sheet.Id, ItemId = itemId, Units = units });
        }
        sheet.StockPending = needed.Sum(n => Math.Max(n.Value - sheet.TakenOf(n.Key), 0));
        return moved;
    }

    /// <summary>
    /// A new charge or container size makes an open sheet add up the item's earlier uses again (its line follows the latest
    /// use), which would change both what it charges and what it took from stock, so it waits until those sheets are discharged.
    /// </summary>
    /// <summary>
    /// A change the uses already recorded would feel: how they add up, or whether they take stock (a sheet takes or returns
    /// stock to match its uses after every change), so it waits for the sheets using the item to be discharged.
    /// </summary>
    private static bool ChangesHowUsesAddUp(ConsumptionPricing? current, ConsumptionCharge charge, decimal? containerMl, bool takenOutBeforehand)
        => (current?.Charge ?? ConsumptionCharge.Unit) != charge
           || (charge == ConsumptionCharge.Container && current?.ContainerMl != containerMl)
           || (current?.TakenOutBeforehand ?? false) != takenOutBeforehand;

    /// <summary>The movement's reason: the sheet and its patient in capitals ("Consumption sheet #12 – TOBY"), cut to fit.</summary>
    private static string StockReason(ConsumptionSheet sheet, string format)
    {
        var patient = sheet.PatientName.Trim().ToUpperInvariant();
        return Texts.T(format, sheet.Id, patient.Length switch { 0 => "—", > 120 => patient[..120], _ => patient });
    }

    private static ConsumptionResult Invalid(string error) => new(ConsumptionStatus.Invalid, Error: error);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
