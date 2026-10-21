using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
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
/// (<see cref="ConsumptionSummary"/>). Stock is not moved here.
/// </summary>
public class ConsumptionService(IUnitOfWork unitOfWork, AuditService audit, TimeProvider clock)
{
    public const decimal MaxQuantity = 10000;

    public async Task<List<ConsumptionSheet>> ListAsync(int closedInLastDays)
        => await unitOfWork.ConsumptionSheets.ListAsync(closedInLastDays > 0 ? clock.GetUtcNow().UtcDateTime.AddDays(-closedInLastDays) : null);

    public async Task<ConsumptionSheet?> GetAsync(int id) => await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(id);

    /// <summary>A sheet for a registered pet (its name and owner are copied) or for a patient typed in.</summary>
    public async Task<ConsumptionResult> OpenAsync(int? petId, string? patientName, string? ownerName, string? notes, string userName)
    {
        var sheet = new ConsumptionSheet { Notes = Clean(notes), OpenedAtUtc = clock.GetUtcNow().UtcDateTime, OpenedBy = userName };
        if (petId is int id)
        {
            var pet = await unitOfWork.Pets.GetByIdAsNoTrackingAsync(id);
            if (pet is null)
                return Invalid(Texts.T("The pet doesn't exist."));
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
            return Invalid(Texts.T("Choose a registered pet or type the patient's name."));

        await unitOfWork.ConsumptionSheets.AddAsync(sheet);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheet.Id, AuditActionType.Add, $"{sheet.PatientName} · {sheet.OwnerName}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
    }

    /// <summary>Records a use: units, or mL for items charged by mL or by container.</summary>
    public async Task<ConsumptionResult> AddEntryAsync(int sheetId, int itemId, decimal quantity, string? notes, string userName)
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

        var entry = new ConsumptionEntry
        {
            SheetId = sheet.Id,
            ItemId = item.Id,
            ItemName = item.Name,
            Charge = charge,
            ContainerMl = charge == ConsumptionCharge.Container ? pricing!.ContainerMl : null,
            UnitPrice = charge == ConsumptionCharge.PerMl ? pricing!.PricePerMl ?? 0 : item.SellPrice,
            Quantity = Math.Round(quantity, 2),
            Notes = Clean(notes),
            UsedAtUtc = clock.GetUtcNow().UtcDateTime,
            CreatedBy = userName
        };
        sheet.Entries.Add(entry);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheet.Id, AuditActionType.Edit, $"+ {entry.ItemName}: {entry.Quantity}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, entry.Id);
    }

    /// <summary>Throws away what is left in the patient's open container of that item; the next use starts a new one.</summary>
    public async Task<ConsumptionResult> DiscardRestAsync(int sheetId, int itemId, string userName)
    {
        var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
        if (sheet is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        if (sheet.IsClosed)
            return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));
        var line = ConsumptionSummary.For(sheet).Lines.FirstOrDefault(l => l.ItemId == itemId);
        if (line is not { Charge: ConsumptionCharge.Container, LeftMl: > 0 })
            return Invalid(Texts.T("There is no open container of this item."));

        var last = sheet.Entries.Where(e => e.ItemId == itemId).MaxBy(e => e.UsedAtUtc)!;
        sheet.Entries.Add(new ConsumptionEntry
        {
            SheetId = sheet.Id, ItemId = itemId, ItemName = last.ItemName, Charge = last.Charge, ContainerMl = last.ContainerMl,
            UnitPrice = last.UnitPrice, Quantity = line.LeftMl, Discarded = true, UsedAtUtc = clock.GetUtcNow().UtcDateTime, CreatedBy = userName
        });
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheet.Id, AuditActionType.Edit, $"Discarded {line.ItemName}: {line.LeftMl}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
    }

    /// <summary>Removes a use recorded by mistake, while the sheet is open.</summary>
    public async Task<ConsumptionResult> RemoveEntryAsync(int sheetId, int entryId, string userName)
    {
        var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(sheetId);
        var entry = sheet?.Entries.FirstOrDefault(e => e.Id == entryId);
        if (sheet is null || entry is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        if (sheet.IsClosed)
            return Invalid(Texts.T("The sheet is closed: reopen it to add or remove supplies."));

        sheet.Entries.Remove(entry);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), sheet.Id, AuditActionType.Edit,
            $"- {entry.ItemName}: {entry.Quantity}{(entry.Discarded ? " (discarded)" : "")} ({entry.CreatedBy}, {entry.UsedAtUtc:u})", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, sheet.Id);
    }

    /// <summary>Discharge: the sheet takes no more entries and what is left in open containers counts as discarded.</summary>
    public async Task<ConsumptionResult> CloseAsync(int id, string userName)
        => await SetClosedAsync(id, closed: true, userName);

    public async Task<ConsumptionResult> ReopenAsync(int id, string userName)
        => await SetClosedAsync(id, closed: false, userName);

    public async Task<ConsumptionResult> DeleteAsync(int id, string userName)
    {
        var sheet = await unitOfWork.ConsumptionSheets.GetWithEntriesAsync(id);
        if (sheet is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        var snapshot = JsonSerializer.Serialize(sheet);
        unitOfWork.ConsumptionSheets.Remove(sheet);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), id, AuditActionType.Delete, snapshot, userName);
        return new ConsumptionResult(ConsumptionStatus.Done, id);
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
    public async Task<ConsumptionResult> SetPricingAsync(int itemId, ConsumptionCharge charge, decimal? containerMl, int? pricePerMl, bool controlled,
        string userName)
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
        var isNew = pricing is null;
        pricing ??= new ConsumptionPricing { ItemId = itemId };
        pricing.Charge = charge;
        pricing.ContainerMl = charge == ConsumptionCharge.Container ? Math.Round(containerMl!.Value, 2) : null;
        pricing.PricePerMl = charge == ConsumptionCharge.PerMl ? pricePerMl : null;
        pricing.Controlled = controlled;
        pricing.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        pricing.UpdatedBy = userName;
        if (isNew)
            await unitOfWork.ConsumptionPricings.AddAsync(pricing);
        else
            unitOfWork.ConsumptionPricings.Update(pricing);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionPricing), pricing.Id, isNew ? AuditActionType.Add : AuditActionType.Edit,
            $"{item.Name}: {charge}, {pricing.ContainerMl} mL, {pricing.PricePerMl}/mL, controlled {controlled}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, pricing.Id);
    }

    /// <summary>The item goes back to being charged per unit at its sale price.</summary>
    public async Task<ConsumptionResult> DeletePricingAsync(int itemId, string userName)
    {
        var pricing = (await unitOfWork.ConsumptionPricings.FindAsync(p => p.ItemId == itemId)).FirstOrDefault();
        if (pricing is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        unitOfWork.ConsumptionPricings.Remove(pricing);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionPricing), pricing.Id, AuditActionType.Delete, $"Item {itemId}", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, pricing.Id);
    }

    private async Task<ConsumptionResult> SetClosedAsync(int id, bool closed, string userName)
    {
        var sheet = await unitOfWork.ConsumptionSheets.GetByIdAsync(id);
        if (sheet is null)
            return new ConsumptionResult(ConsumptionStatus.NotFound);
        if (sheet.IsClosed == closed)
            return new ConsumptionResult(ConsumptionStatus.Done, id);
        sheet.ClosedAtUtc = closed ? clock.GetUtcNow().UtcDateTime : null;
        sheet.ClosedBy = closed ? userName : null;
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(ConsumptionSheet), id, AuditActionType.Edit, closed ? "Closed (discharge)" : "Reopened", userName);
        return new ConsumptionResult(ConsumptionStatus.Done, id);
    }

    private static ConsumptionResult Invalid(string error) => new(ConsumptionStatus.Invalid, Error: error);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
