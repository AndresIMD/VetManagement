using System.Text.Json;
using VetManagement.Application.Configuration;
using VetManagement.Application.Inventory;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Scheduling;
using VetManagement.Application.Services;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Billing;

/// <summary>The clinic's billing policy (key "billing"); defaults are usable as they are.</summary>
public class BillingSettingsService(IUnitOfWork unitOfWork, AuditService audit)
    : ClinicSettingsService<BillingSettings>(unitOfWork, audit)
{
    protected override string SettingsKey => "billing";
    protected override BillingSettings CreateDefaults() => new();
    protected override List<string> Validate(BillingSettings settings) => settings.Validate();
}

public enum BillingStatus
{
    Done,
    NotFound,
    Invalid,
    /// <summary>Already exists (the appointment's sale, the day's close).</summary>
    Conflict
}

public sealed record BillingResult(BillingStatus Status, int? Id = null, string? Error = null);

public sealed record NewSale(int? ClientId, int? PetId, int? AppointmentId, string? CustomerName, string? Notes);

/// <summary>Description and unit price may be left empty for services and products: the catalog fills them.</summary>
public sealed record NewSaleLine(SaleLineKind Kind, string? Description, string? ServiceCode, int? ItemId, int Quantity, int? UnitPrice, int Discount);

public sealed record MethodTotal(string Method, string Name, bool IsCash, int Amount, int Count);

public sealed record DaySummary(DateOnly Date, IReadOnlyList<MethodTotal> Totals, int ExpectedCash, CashClose? Close);

/// <summary>
/// Charging at the clinic: sales, payments, voids and the daily cash close. Every write runs under one
/// clinic-wide lock, so two payments can't overpay a sale and no payment slips past a day being closed.
/// </summary>
public class BillingService(
    IUnitOfWork unitOfWork,
    BillingSettingsService billingSettings,
    SchedulingSettingsService schedulingSettings,
    AuditService audit,
    TimeProvider clock)
{
    private const string EntityName = "Sale";
    // Same lock as every automatic stock change: paying a sale moves stock, and no payment slips past a day being closed.
    private const string LockKey = Stock.LockKey;

    public async Task<DateOnly> TodayAsync() => await BusinessDateAsync(clock.GetUtcNow().UtcDateTime);

    public async Task<List<Sale>> ListAsync(DateOnly from, DateOnly to, SaleStatus? status)
        => await unitOfWork.Sales.ListAsync(from, to, status);

    public Task<Sale?> GetAsync(int id) => unitOfWork.Sales.GetWithDetailsAsync(id);

    /// <summary>Opens a sale. From an appointment it starts with the service and the deposit paid online.</summary>
    public async Task<BillingResult> CreateAsync(NewSale request, string userName)
    {
        var settings = (await billingSettings.GetAsync()).Settings;
        if (!settings.Enabled)
            return new BillingResult(BillingStatus.Invalid, Error: "Billing is disabled for this clinic.");

        var agenda = (await schedulingSettings.GetAsync()).Settings;
        var now = clock.GetUtcNow().UtcDateTime;
        var today = await BusinessDateAsync(now);

        var result = await unitOfWork.ExecuteExclusiveAsync(LockKey, async () =>
        {
            var sale = new Sale
            {
                ClientId = request.ClientId,
                PetId = request.PetId,
                CustomerName = request.CustomerName?.Trim() ?? string.Empty,
                Notes = request.Notes,
                BusinessDate = today,
                CreatedAtUtc = now,
                CreatedBy = userName
            };

            if (request.AppointmentId is int appointmentId)
            {
                var appointment = await unitOfWork.Appointments.GetByIdAsync(appointmentId);
                if (appointment is null)
                    return new BillingResult(BillingStatus.NotFound);
                if (await unitOfWork.Sales.GetActiveByAppointmentAsync(appointmentId) is { } existing)
                    return new BillingResult(BillingStatus.Conflict, existing.Id, "This appointment already has a sale.");
                if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.PendingPayment)
                    return new BillingResult(BillingStatus.Invalid, Error: $"A {appointment.Status} appointment can't be charged.");

                sale.AppointmentId = appointmentId;
                sale.ClientId ??= appointment.ClientId;
                sale.PetId ??= appointment.PetId;
                if (sale.CustomerName.Length == 0)
                    sale.CustomerName = appointment.OwnerName;

                var service = agenda.Services.FirstOrDefault(s => string.Equals(s.Code, appointment.ServiceCode, StringComparison.OrdinalIgnoreCase));
                sale.AddLine(new SaleLine
                {
                    Kind = SaleLineKind.Service,
                    ServiceCode = appointment.ServiceCode,
                    Description = service?.Name ?? appointment.ServiceCode,
                    Quantity = 1,
                    UnitPrice = appointment.Price
                });
                if (appointment.DepositStatus == DepositStatus.Paid && appointment.PaidAmount > 0)
                    sale.AddPayment(new SalePayment
                    {
                        Method = SalePayment.OnlineDepositMethod,
                        Amount = Math.Min(appointment.PaidAmount, sale.Balance),
                        Reference = appointment.PaymentProvider,
                        ReceivedAtUtc = now,
                        BusinessDate = today,
                        ReceivedBy = userName
                    });
            }

            if (sale.CustomerName.Length == 0 && sale.ClientId is int clientId && await unitOfWork.Clients.GetByIdAsync(clientId) is { } client)
                sale.CustomerName = $"{client.Name} {client.LastName}".Trim();
            if (sale.CustomerName.Length == 0)
                return new BillingResult(BillingStatus.Invalid, Error: "Enter the customer's name or pick a client.");

            await unitOfWork.Sales.AddAsync(sale);
            await unitOfWork.SaveChangesAsync();
            return new BillingResult(BillingStatus.Done, sale.Id);
        });

        if (result.Status == BillingStatus.Done)
            await audit.LogAsync(EntityName, result.Id!.Value, AuditActionType.Add, JsonSerializer.Serialize(request), userName);
        return result;
    }

    /// <param name="canExceedDiscount">The user may give discounts above the clinic's limit.</param>
    public async Task<BillingResult> AddLineAsync(int saleId, NewSaleLine request, bool canExceedDiscount, string userName)
    {
        var settings = (await billingSettings.GetAsync()).Settings;
        var agenda = (await schedulingSettings.GetAsync()).Settings;

        var result = await unitOfWork.ExecuteExclusiveAsync(LockKey, async () =>
        {
            var sale = await unitOfWork.Sales.GetWithDetailsAsync(saleId);
            if (sale is null)
                return new BillingResult(BillingStatus.NotFound);

            var line = new SaleLine
            {
                Kind = request.Kind,
                Description = request.Description?.Trim() ?? string.Empty,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice ?? 0,
                Discount = request.Discount
            };

            switch (request.Kind)
            {
                case SaleLineKind.Service:
                    var service = agenda.Services.FirstOrDefault(s => string.Equals(s.Code, request.ServiceCode, StringComparison.OrdinalIgnoreCase));
                    if (service is null)
                        return new BillingResult(BillingStatus.Invalid, Error: $"Unknown service '{request.ServiceCode}'.");
                    line.ServiceCode = service.Code;
                    if (line.Description.Length == 0) line.Description = service.Name;
                    line.UnitPrice = request.UnitPrice ?? service.Price;
                    break;

                case SaleLineKind.Product:
                    var item = request.ItemId is int itemId ? await unitOfWork.Items.GetByIdAsync(itemId) : null;
                    if (item is null)
                        return new BillingResult(BillingStatus.Invalid, Error: "Pick an inventory item for a product line.");
                    line.ItemId = item.Id;
                    if (line.Description.Length == 0) line.Description = item.Name;
                    line.UnitPrice = request.UnitPrice ?? item.SellPrice;
                    var alreadyInSale = sale.Lines.Where(l => l.ItemId == item.Id).Sum(l => l.Quantity);
                    if (settings.DeductStockOnSale && alreadyInSale + line.Quantity > item.Stock)
                        return new BillingResult(BillingStatus.Invalid, Error: $"Only {item.Stock - alreadyInSale} of '{item.Name}' left in stock.");
                    break;

                default:
                    if (request.UnitPrice is null)
                        return new BillingResult(BillingStatus.Invalid, Error: "Enter the price.");
                    break;
            }

            if (!canExceedDiscount && line.Discount * 100L > line.Gross * (long)settings.MaxDiscountPercent)
                return new BillingResult(BillingStatus.Invalid,
                    Error: $"Discounts above {settings.MaxDiscountPercent}% need an administrator.");

            if (sale.AddLine(line) is { } error)
                return new BillingResult(BillingStatus.Invalid, Error: error);

            await unitOfWork.SaveChangesAsync();
            return new BillingResult(BillingStatus.Done, saleId);
        });

        if (result.Status == BillingStatus.Done)
            await audit.LogAsync(EntityName, saleId, AuditActionType.Edit, $"Line added: {JsonSerializer.Serialize(request)}", userName);
        return result;
    }

    public async Task<BillingResult> RemoveLineAsync(int saleId, int lineId, string userName)
    {
        var result = await unitOfWork.ExecuteExclusiveAsync(LockKey, async () =>
        {
            var sale = await unitOfWork.Sales.GetWithDetailsAsync(saleId);
            if (sale is null)
                return new BillingResult(BillingStatus.NotFound);
            if (sale.RemoveLine(lineId) is { } error)
                return new BillingResult(error == "Line not found." ? BillingStatus.NotFound : BillingStatus.Invalid, Error: error);

            await unitOfWork.SaveChangesAsync();
            return new BillingResult(BillingStatus.Done, saleId);
        });

        if (result.Status == BillingStatus.Done)
            await audit.LogAsync(EntityName, saleId, AuditActionType.Edit, $"Line {lineId} removed", userName);
        return result;
    }

    /// <summary>Takes a payment; when the sale is fully paid its products leave the inventory (if enabled).</summary>
    public async Task<BillingResult> AddPaymentAsync(int saleId, string method, int amount, string? reference, string userName)
    {
        var settings = (await billingSettings.GetAsync()).Settings;
        var definition = settings.FindMethod(method);
        if (definition is not { Enabled: true })
            return new BillingResult(BillingStatus.Invalid, Error: $"Payment method '{method}' is not enabled.");

        var now = clock.GetUtcNow().UtcDateTime;
        var today = await BusinessDateAsync(now);

        var result = await unitOfWork.ExecuteExclusiveAsync(LockKey, async () =>
        {
            var sale = await unitOfWork.Sales.GetWithDetailsAsync(saleId);
            if (sale is null)
                return new BillingResult(BillingStatus.NotFound);
            if (await IsClosedAsync(today))
                return new BillingResult(BillingStatus.Invalid, Error: $"The cash for {today:yyyy-MM-dd} is already closed.");

            var error = sale.AddPayment(new SalePayment
            {
                Method = definition.Code,
                Amount = amount,
                Reference = reference,
                ReceivedAtUtc = now,
                BusinessDate = today,
                ReceivedBy = userName
            });
            if (error is not null)
                return new BillingResult(BillingStatus.Invalid, Error: error);

            if (sale.Status == SaleStatus.Paid && settings.DeductStockOnSale)
                await MoveStockAsync(sale, InventoryMovementType.Egress, userName, now);

            await unitOfWork.SaveChangesAsync();
            return new BillingResult(BillingStatus.Done, saleId);
        });

        if (result.Status == BillingStatus.Done)
            await audit.LogAsync(EntityName, saleId, AuditActionType.Edit, $"Payment {definition.Code} {amount}", userName);
        return result;
    }

    /// <summary>Cancels a sale and puts its products back in stock. Days already closed stay untouched.</summary>
    public async Task<BillingResult> VoidAsync(int saleId, string reason, string userName)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var result = await unitOfWork.ExecuteExclusiveAsync(LockKey, async () =>
        {
            var sale = await unitOfWork.Sales.GetWithDetailsAsync(saleId);
            if (sale is null)
                return new BillingResult(BillingStatus.NotFound);
            foreach (var date in sale.Payments.Select(p => p.BusinessDate).Distinct())
                if (await IsClosedAsync(date))
                    return new BillingResult(BillingStatus.Invalid, Error: $"It has payments in a closed day ({date:yyyy-MM-dd}).");

            if (sale.Void(reason, userName, now) is { } error)
                return new BillingResult(BillingStatus.Invalid, Error: error);
            if (sale.StockDeducted)
                await MoveStockAsync(sale, InventoryMovementType.Ingress, userName, now);

            await unitOfWork.SaveChangesAsync();
            return new BillingResult(BillingStatus.Done, saleId);
        });

        if (result.Status == BillingStatus.Done)
            await audit.LogAsync(EntityName, saleId, AuditActionType.Delete, $"Voided: {reason}", userName);
        return result;
    }

    public async Task<DaySummary> GetDaySummaryAsync(DateOnly date)
    {
        var settings = (await billingSettings.GetAsync()).Settings;
        var payments = await unitOfWork.Sales.GetPaymentsAsync(date, date);
        var close = (await unitOfWork.CashCloses.FindAsync(c => c.BusinessDate == date)).FirstOrDefault();
        return Summarize(date, payments, settings, close);
    }

    /// <summary>Records the day's cash count. After it, the day takes no payments and its sales can't be voided.</summary>
    public async Task<BillingResult> CloseDayAsync(DateOnly date, int countedCash, string? notes, string userName)
    {
        var settings = (await billingSettings.GetAsync()).Settings;
        var now = clock.GetUtcNow().UtcDateTime;
        if (date > await BusinessDateAsync(now))
            return new BillingResult(BillingStatus.Invalid, Error: "A future day can't be closed.");
        if (countedCash < 0)
            return new BillingResult(BillingStatus.Invalid, Error: "The counted cash can't be negative.");

        var result = await unitOfWork.ExecuteExclusiveAsync(LockKey, async () =>
        {
            if (await IsClosedAsync(date))
                return new BillingResult(BillingStatus.Conflict, Error: $"{date:yyyy-MM-dd} is already closed.");

            var summary = Summarize(date, await unitOfWork.Sales.GetPaymentsAsync(date, date), settings, null);
            var close = new CashClose
            {
                BusinessDate = date,
                ExpectedCash = summary.ExpectedCash,
                CountedCash = countedCash,
                TotalsJson = JsonSerializer.Serialize(summary.Totals),
                Notes = notes,
                ClosedAtUtc = now,
                ClosedBy = userName
            };
            await unitOfWork.CashCloses.AddAsync(close);
            await unitOfWork.SaveChangesAsync();
            return new BillingResult(BillingStatus.Done, close.Id);
        });

        if (result.Status == BillingStatus.Done)
            await audit.LogAsync(nameof(CashClose), result.Id!.Value, AuditActionType.Add, $"Closed {date:yyyy-MM-dd}, counted {countedCash}", userName);
        return result;
    }

    private static DaySummary Summarize(DateOnly date, List<SalePayment> payments, BillingSettings settings, CashClose? close)
    {
        var totals = payments
            .GroupBy(p => p.Method, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var definition = settings.FindMethod(g.Key);
                var name = g.Key == SalePayment.OnlineDepositMethod ? "Abono online" : definition?.Name ?? g.Key;
                return new MethodTotal(g.Key, name, definition?.IsCash == true, g.Sum(p => p.Amount), g.Count());
            })
            .OrderBy(t => t.Method)
            .ToList();
        return new DaySummary(date, totals, totals.Where(t => t.IsCash).Sum(t => t.Amount), close);
    }

    private async Task<bool> IsClosedAsync(DateOnly date)
        => (await unitOfWork.CashCloses.FindAsync(c => c.BusinessDate == date)).Any();

    private async Task MoveStockAsync(Sale sale, InventoryMovementType type, string userName, DateTime nowUtc)
    {
        // An item deleted from the catalog since has nothing to move.
        foreach (var line in sale.Lines.Where(l => l.Kind == SaleLineKind.Product && l.ItemId is not null))
            await Stock.MoveAsync(unitOfWork, line.ItemId!.Value, line.Quantity, type,
                type == InventoryMovementType.Egress ? $"Sale #{sale.Id}" : $"Sale #{sale.Id} voided", userName, nowUtc);
        sale.StockDeducted = type == InventoryMovementType.Egress;
    }

    private async Task<DateOnly> BusinessDateAsync(DateTime utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById((await schedulingSettings.GetAsync()).Settings.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
    }
}
