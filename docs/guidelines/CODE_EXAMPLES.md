# VetManagement - Detailed Code Examples & Fixes

**Purpose**: Provide exact code snippets for implementing critical fixes  
**Format**: Before/After comparisons with explanations

---

## Fix #1: Refactor AuditService to Repository Pattern

### Problem
Loading 100K+ audit logs into memory, filtering client-side = memory crash + network bloat

### Before (Anti-Pattern)
```csharp
// VetManagement.Application/Services/AuditService.cs - CURRENT
public async Task<List<AuditLog>> GetLogsFilteredAsync(
    int? entityId = null,
    string? entityName = null,
    DateTime? from = null,
    DateTime? to = null,
    AuditActionType? action = null,
    string? user = null)
{
    var allLogs = await unitOfWork.AuditLogs.GetAllAsync(); // 🔴 LOADS ENTIRE TABLE
    var query = allLogs.AsEnumerable(); // 🔴 SWITCHES TO LINQ-TO-OBJECTS

    if (entityId.HasValue)
        query = query.Where(l => l.EntityId == entityId.Value);
    if (!string.IsNullOrWhiteSpace(entityName))
        query = query.Where(l => l.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));
    if (from.HasValue)
        query = query.Where(l => l.Date >= from.Value);
    if (to.HasValue)
        query = query.Where(l => l.Date <= to.Value);
    if (action.HasValue)
        query = query.Where(l => l.Action == action.Value);
    if (!string.IsNullOrWhiteSpace(user))
        query = query.Where(l => (l.User ?? string.Empty).Contains(user, StringComparison.OrdinalIgnoreCase));

    return query.OrderByDescending(l => l.Date).ToList();
}
```

**Issues**:
- ❌ **N+1 Problem**: Gets all rows regardless of filters
- ❌ **Memory Leak**: 100K audit logs in RAM simultaneously
- ❌ **No Pagination**: Can't handle large datasets
- ❌ **DB Unused**: Indexes/constraints not used
- ❌ **Network Bloat**: Entire dataset sent over API

---

### After (Correct Pattern)
```csharp
// VetManagement.Infrastructure/Repositories/AuditLogRepository.cs - NEW METHOD
public async Task<(List<AuditLog> Items, int TotalCount)> GetFilteredPagedAsync(
    int page = 0,
    int pageSize = 25,
    int? entityId = null,
    string? entityName = null,
    AuditActionType? action = null,
    string? user = null,
    DateTime? from = null,
    DateTime? to = null)
{
    // ✅ Start with readonly query
    var query = _context.AuditLogs.AsNoTracking();

    // ✅ Apply filters IN DATABASE
    if (entityId.HasValue)
        query = query.Where(l => l.EntityId == entityId.Value);

    if (!string.IsNullOrWhiteSpace(entityName))
        query = query.Where(l => l.EntityName == entityName); // Exact match (case-insensitive at DB level)

    if (action.HasValue)
        query = query.Where(l => l.Action == action.Value);

    if (!string.IsNullOrWhiteSpace(user))
        query = query.Where(l => (l.User ?? string.Empty).Contains(user));

    if (from.HasValue)
        query = query.Where(l => l.Date >= from.Value);

    if (to.HasValue)
        query = query.Where(l => l.Date <= to.Value);

    // ✅ Get total AFTER filters but BEFORE pagination
    var totalCount = await query.CountAsync();

    // ✅ Sort, then paginate (pagination must be LAST)
    var items = await query
        .OrderByDescending(l => l.Date)
        .Skip(page * pageSize)
        .Take(pageSize)
        .ToListAsync(); // ✅ Execute in database, get only pageSize rows

    return (items, totalCount);
}

// Update IRepository interface
public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(List<AuditLog> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? entityId = null,
        string? entityName = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null);
}
```

```csharp
// VetManagement.Application/Services/AuditService.cs - SIMPLIFIED
public async Task<PagedResult<AuditLog>> GetLogsPagedAsync(
    int page = 0,
    int pageSize = 25,
    string? entityName = null,
    AuditActionType? action = null,
    string? user = null,
    DateTime? from = null,
    DateTime? to = null,
    string? itemName = null, // Client-side filter (see below)
    CancellationToken ct = default)
{
    // ✅ Delegate to repository (server-side filtering)
    var (items, totalCount) = await unitOfWork.AuditLogs.GetFilteredPagedAsync(
        page: page,
        pageSize: pageSize,
        entityName: entityName,
        action: action,
        user: user,
        from: from,
        to: to);

    // ⚠️ ONLY for non-indexed filters: Client-side filter by item name
    // (We can't easily join to Item table, so filter in-memory after pagination)
    if (!string.IsNullOrWhiteSpace(itemName))
    {
        items = items.Where(log =>
        {
            // This would require a join in real scenario
            // For now, stub - in production, load related Item data via Include
            return log.EntityName.Contains(itemName, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    return new PagedResult<AuditLog>(
        items: items,
        totalCount: totalCount,
        page: page,
        pageSize: pageSize);
}

// ✅ Delete old method
// [Obsolete("Use GetLogsPagedAsync instead")]
// public async Task<List<AuditLog>> GetLogsFilteredAsync(...) { }
```

---

### Benefits
✅ **Performance**: 100K logs, filter returns 50 → only 50 loaded  
✅ **Memory**: RAM usage constant regardless of table size  
✅ **Database**: Indexes used, query optimizer effective  
✅ **API**: Network transfer minimal  
✅ **Scalable**: Works with 1M+ rows  

---

## Fix #2: Create StockAlertFilter Enum (Type Safety)

### Before (Anti-Pattern)
```csharp
// ItemRepository.cs - CURRENT
public async Task<(List<Item> Items, int TotalCount)> GetPagedListAsync(
    string? searchTerm,
    ItemType? type,
    int page,
    int pageSize,
    string? alert, // 🔴 MAGIC STRINGS: "low", "zero", null
    ItemSortField? sortBy = null,
    SortDirection sortDirection = SortDirection.Ascending)
{
    var query = _context.Items.AsNoTracking();

    // 🔴 Magic strings - typo-prone
    if (alert == "low")
    {
        query = query.Where(i => i.LowStockThreshold > 0 && i.Stock > 0 && i.Stock <= i.LowStockThreshold);
    }
    // 🔴 What if someone passes "Low" instead of "low"? Silent bug
    if (alert == "zero")
    {
        query = query.Where(i => i.Stock == 0);
    }
    // ... rest of method
}
```

**Issues**:
- ❌ **Typo-Prone**: `"lo"` compiles but silently fails
- ❌ **No IntelliSense**: Developer must remember exact string
- ❌ **Hard to Refactor**: IDE can't find all references
- ❌ **Inconsistent**: Case-sensitive (`"Low"` vs `"low"`)

---

### After (Type-Safe)
```csharp
// VetManagement.Staff.UI/Enums/InventoryEnums.cs - ADD NEW ENUM
namespace VetManagement.Staff.UI.Enums;

/// <summary>
/// Represents stock alert filter criteria for item inventory.
/// </summary>
public enum StockAlertFilter
{
    [DisplayString("None")]
    None = 0,

    [DisplayString("Low Stock")]
    LowStock = 1,

    [DisplayString("Out of Stock")]
    OutOfStock = 2
}
```

```csharp
// VetManagement.Infrastructure/Repositories/ItemRepository.cs - UPDATED
public async Task<(List<Item> Items, int TotalCount)> GetPagedListAsync(
    string? searchTerm,
    ItemType? type,
    int page,
    int pageSize,
    StockAlertFilter? alertFilter = null, // ✅ TYPE-SAFE
    ItemSortField? sortBy = null,
    SortDirection sortDirection = SortDirection.Ascending)
{
    var query = _context.Items.AsNoTracking();

    // Apply search and type filters...
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        var term = searchTerm.Trim();
        query = query.Where(i =>
            i.Name.Contains(term) ||
            (i.Brand != null && i.Brand.Contains(term)) ||
            (i.Barcode != null && i.Barcode.Contains(term)) ||
            (i.BrandBarcode != null && i.BrandBarcode.Contains(term)) ||
            (i is Drug && ((Drug)i).Compound.Contains(term))
        );
    }

    if (type.HasValue && type.Value != ItemType.None)
    {
        query = query.Where(i => i.Type == type.Value);
    }

    // ✅ TYPE-SAFE alert filtering using enum switch
    if (alertFilter.HasValue && alertFilter.Value != StockAlertFilter.None)
    {
        query = alertFilter.Value switch
        {
            StockAlertFilter.LowStock => query.Where(i =>
                i.LowStockThreshold > 0 && i.Stock > 0 && i.Stock <= i.LowStockThreshold),

            StockAlertFilter.OutOfStock => query.Where(i =>
                i.Stock == 0),

            _ => query // Should never reach (handled by HasValue check above)
        };
    }

    // Sorting, pagination, return...
    // (same as before, but with new ItemSortField values from Fix #3)
}
```

```csharp
// VetManagement.Api/Controllers/Inventory/ItemsController.cs - UPDATED ENDPOINT
[HttpGet("paged")]
public async Task<ActionResult<PagedResult<Item>>> GetPagedAsync(
    [FromQuery] int page = 0,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? searchTerm = null,
    [FromQuery] ItemType? type = null,
    [FromQuery] StockAlertFilter? alertFilter = null, // ✅ ASP.NET auto-parses enum
    [FromQuery] ItemSortField? sortBy = null,
    [FromQuery] SortDirection sortDirection = SortDirection.Ascending)
{
    var (items, totalCount) = await queryService.GetPagedItemsAsync(
        searchTerm: searchTerm,
        type: type,
        page: page,
        pageSize: pageSize,
        alertFilter: alertFilter,
        sortBy: sortBy,
        sortDirection: sortDirection);

    return Ok(new PagedResult<Item>(items, totalCount, page, pageSize));
}
```

```razor
<!-- VetManagement.Staff.UI/Pages/Items/InventoryItems.razor - UPDATED FILTER -->
@page "/inventory/items"
@using VetManagement.Staff.UI.Enums

<MudSelect T="StockAlertFilter?" 
           Label="Stock Alert" 
           @bind-Value="FilterAlert"
           Clearable="true"
           Variant="Variant.Outlined">
    <MudSelectItem Value="@((StockAlertFilter?)null)">All</MudSelectItem>
    <MudSelectItem Value="@StockAlertFilter.LowStock">Low Stock</MudSelectItem>
    <MudSelectItem Value="@StockAlertFilter.OutOfStock">Out of Stock</MudSelectItem>
</MudSelect>

@code {
    private StockAlertFilter? _filterAlert;

    private StockAlertFilter? FilterAlert
    {
        get => _filterAlert;
        set
        {
            if (_filterAlert != value)
            {
                _filterAlert = value;
                _ = (_table?.ReloadServerData() ?? Task.CompletedTask);
            }
        }
    }

    private async Task<TableData<Item>> LoadServerData(TableState state, CancellationToken ct)
    {
        var result = await ItemService.GetPagedAsync(
            page: state.Page,
            pageSize: state.PageSize,
            alertFilter: FilterAlert, // ✅ Strongly typed
            ...);

        return new TableData<Item> 
        { 
            Items = result.Items, 
            TotalItems = result.TotalCount 
        };
    }
}
```

---

### Benefits
✅ **Compile-Time Safety**: Typos caught at build time  
✅ **IntelliSense**: Full IDE support, autocomplete  
✅ **Refactorable**: IDE can rename enum values everywhere  
✅ **Self-Documenting**: Code intent is clear  
✅ **Testable**: Enum values are discrete, easy to test all cases  

---

## Fix #3: Expand ItemSortField Enum

### Current (Incomplete)
```csharp
public enum ItemSortField
{
    Name,
    Type,
    Brand,
    Stock,
    SellPrice,
    Barcode
}
```

### Updated (Complete)
```csharp
namespace VetManagement.Staff.UI.Enums;

/// <summary>
/// Field to sort items by in paged queries.
/// </summary>
public enum ItemSortField
{
    Id,
    Name,
    Type,
    Brand,
    Stock,
    SellPrice,
    BuyPrice,
    LowStockThreshold,
    Barcode,
    BrandBarcode
}
```

### Update Repository Sorting Logic
```csharp
// VetManagement.Infrastructure/Repositories/ItemRepository.cs
private IQueryable<Item> ApplySorting(
    IQueryable<Item> query,
    ItemSortField? sortBy,
    SortDirection direction)
{
    if (!sortBy.HasValue)
        return query.OrderBy(i => i.Id);

    var isDescending = direction == SortDirection.Descending;

    return sortBy.Value switch
    {
        ItemSortField.Id => isDescending ? query.OrderByDescending(i => i.Id) : query.OrderBy(i => i.Id),
        ItemSortField.Name => isDescending ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name),
        ItemSortField.Type => isDescending ? query.OrderByDescending(i => i.Type) : query.OrderBy(i => i.Type),
        ItemSortField.Brand => isDescending ? query.OrderByDescending(i => i.Brand) : query.OrderBy(i => i.Brand),
        ItemSortField.Stock => isDescending ? query.OrderByDescending(i => i.Stock) : query.OrderBy(i => i.Stock),
        ItemSortField.SellPrice => isDescending ? query.OrderByDescending(i => i.SellPrice) : query.OrderBy(i => i.SellPrice),
        ItemSortField.BuyPrice => isDescending ? query.OrderByDescending(i => i.BuyPrice) : query.OrderBy(i => i.BuyPrice),
        ItemSortField.LowStockThreshold => isDescending ? query.OrderByDescending(i => i.LowStockThreshold) : query.OrderBy(i => i.LowStockThreshold),
        ItemSortField.Barcode => isDescending ? query.OrderByDescending(i => i.Barcode) : query.OrderBy(i => i.Barcode),
        ItemSortField.BrandBarcode => isDescending ? query.OrderByDescending(i => i.BrandBarcode) : query.OrderBy(i => i.BrandBarcode),
        _ => query.OrderBy(i => i.Id)
    };
}
```

---

## Fix #4: Centralize Enum Mapping

### Before (Duplicated)
```csharp
// VetManagement.Application/Services/InventoryMovementService.cs
public async Task AddMovementAsync(InventoryMovement movement, string userName)
{
    // ... setup code ...

    // 🔴 DUPLICATED MAPPING LOGIC
    var auditAction = movement.Type switch
    {
        InventoryMovementType.Ingress => AuditActionType.Ingress,
        InventoryMovementType.Egress => AuditActionType.Egress,
        InventoryMovementType.Adjustment => AuditActionType.Adjustment,
        InventoryMovementType.MassiveStockIngress => AuditActionType.MassiveStockIngress,
        InventoryMovementType.MassiveStockEgress => AuditActionType.MassiveStockEgress,
        _ => AuditActionType.None
    };

    await LogAuditAsync(..., auditAction, ...);
}

// Imagine this same switch expression in ItemService, ExamService, etc...
// 🔴 If enums change, you update in 5 places
```

---

### After (Single Source of Truth)
```csharp
// VetManagement.Staff.UI/Helpers/AuditActionHelper.cs - EXPANDED
namespace VetManagement.Staff.UI.Helpers;

public static class AuditActionHelper
{
    /// <summary>
    /// Maps an inventory movement type to its corresponding audit action.
    /// </summary>
    public static AuditActionType? MapMovementTypeToAudit(InventoryMovementType movementType) =>
        movementType switch
        {
            InventoryMovementType.None => null,
            InventoryMovementType.Ingress => AuditActionType.Ingress,
            InventoryMovementType.Egress => AuditActionType.Egress,
            InventoryMovementType.Adjustment => AuditActionType.Adjustment,
            InventoryMovementType.MassiveStockIngress => AuditActionType.MassiveStockIngress,
            InventoryMovementType.MassiveStockEgress => AuditActionType.MassiveStockEgress,
            _ => null
        };

    // ... existing GetInventoryActions(), GetSecurityActions(), GetCrudActions() ...
}
```

```csharp
// VetManagement.Application/Services/InventoryMovementService.cs - SIMPLIFIED
public async Task AddMovementAsync(InventoryMovement movement, string userName)
{
    movement.Responsible = userName;
    if (movement.Date == default)
        movement.Date = DateTime.UtcNow;

    await unitOfWork.InventoryMovements.AddAsync(movement);
    await unitOfWork.SaveChangesAsync();

    // ✅ USE HELPER INSTEAD OF DUPLICATING SWITCH
    var auditAction = AuditActionHelper.MapMovementTypeToAudit(movement.Type);
    if (auditAction.HasValue)
    {
        await LogAuditAsync(
            nameof(InventoryMovement),
            movement.Id,
            auditAction.Value,
            JsonSerializer.Serialize(movement),
            userName);
    }

    if (notificationService is not null)
    {
        await notificationService.NotifyEntityChangedAsync<InventoryMovement>(
            movement.Id,
            "Add",
            new { movement.ItemId, movement.Type });
        await notificationService.NotifyCollectionChangedAsync<InventoryMovement>("Add");
    }
}
```

---

### Benefits
✅ **DRY**: One mapping definition  
✅ **Maintainable**: Update one place, works everywhere  
✅ **Safe Refactoring**: IDE finds all usages  
✅ **Testable**: Single method to unit test  

---

## Fix #5: Create ValidationHelper

### File: `VetManagement.Staff.UI/Helpers/ValidationHelper.cs`
```csharp
namespace VetManagement.Staff.UI.Helpers;

using VetManagement.Staff.UI.Constants;
using VetManagement.Staff.UI.Enums;
using VetManagement.Staff.UI.Models.Core;
using VetManagement.Staff.UI.Models.Exams;
using VetManagement.Staff.UI.Models.Inventory;

/// <summary>
/// Centralized validation logic for domain models.
/// Ensures consistency between server and client validation.
/// </summary>
public static class ValidationHelper
{
    #region Item Validation

    /// <summary>
    /// Validates item data before persistence.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateItem(Item item)
    {
        if (item == null)
            return (false, "Item cannot be null.");

        if (string.IsNullOrWhiteSpace(item.Name))
            return (false, "Item name is required.");

        if (item.Type == ItemType.None)
            return (false, "Item type must be specified.");

        if (item.Stock < 0)
            return (false, "Stock cannot be negative.");

        if (item.SellPrice < 0)
            return (false, "Sell price cannot be negative.");

        if (item.BuyPrice < 0)
            return (false, "Buy price cannot be negative.");

        if (string.IsNullOrWhiteSpace(item.Barcode))
            return (false, "Barcode is required.");

        if (item.Barcode.Length > 100)
            return (false, "Barcode cannot exceed 100 characters.");

        if (item.LowStockThreshold < 0)
            return (false, "Low stock threshold cannot be negative.");

        // Drug-specific validation
        if (item is Drug drug)
        {
            var drugValidation = ValidateDrug(drug);
            if (!drugValidation.IsValid)
                return drugValidation;
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateDrug(Drug drug)
    {
        if (string.IsNullOrWhiteSpace(drug.Compound))
            return (false, "Drug compound is required.");

        if (drug.ML <= 0)
            return (false, "Drug volume must be greater than 0.");

        if (drug.DosageAmount <= 0)
            return (false, "Dosage amount must be greater than 0.");

        if (string.IsNullOrWhiteSpace(drug.DosageUnit))
            return (false, "Dosage unit is required.");

        return (true, null);
    }

    #endregion

    #region Exam Validation

    /// <summary>
    /// Validates exam data before persistence.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateExam(Exam exam)
    {
        if (exam == null)
            return (false, "Exam cannot be null.");

        if (string.IsNullOrWhiteSpace(exam.Name))
            return (false, "Exam name is required.");

        if (string.IsNullOrWhiteSpace(exam.Brand))
            return (false, "Exam brand is required.");

        if (string.IsNullOrWhiteSpace(exam.Machine))
            return (false, "Exam machine is required.");

        if (exam.SampleType == SampleType.None)
            return (false, "Sample type must be specified.");

        if (exam.SampleContainer == SampleContainer.None)
            return (false, "Sample container must be specified.");

        if (exam.BuyPrice < 0)
            return (false, "Buy price cannot be negative.");

        if (exam.SellPrice < 0)
            return (false, "Sell price cannot be negative.");

        return (true, null);
    }

    #endregion

    #region Inventory Movement Validation

    /// <summary>
    /// Validates inventory movement data.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateInventoryMovement(InventoryMovement movement)
    {
        if (movement == null)
            return (false, "Movement cannot be null.");

        if (movement.ItemId <= 0)
            return (false, "Invalid item reference.");

        if (movement.Quantity == 0)
            return (false, "Quantity cannot be zero.");

        if (movement.Type == InventoryMovementType.None)
            return (false, "Movement type must be specified.");

        if (movement.Quantity < 0 && movement.Type != InventoryMovementType.Egress && movement.Type != InventoryMovementType.MassiveStockEgress)
            return (false, "Negative quantities only allowed for egress movements.");

        return (true, null);
    }

    /// <summary>
    /// Validates stock adjustment amount.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateStockAdjustment(int currentStock, int adjustmentAmount)
    {
        if (adjustmentAmount == 0)
            return (false, "Adjustment amount must be non-zero.");

        int resultingStock = currentStock + adjustmentAmount;
        if (resultingStock < 0)
            return (false, "Adjustment would result in negative stock.");

        return (true, null);
    }

    #endregion

    #region Stock Threshold Validation

    /// <summary>
    /// Validates low stock threshold value.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateLowStockThreshold(int threshold, int currentStock)
    {
        if (threshold < 0)
            return (false, "Threshold cannot be negative.");

        if (threshold > currentStock * 2)
            return (false, "Threshold seems unreasonably high relative to current stock.");

        return (true, null);
    }

    #endregion
}
```

### Usage in ItemService
```csharp
// VetManagement.Application/Services/ItemService.cs
public async Task<bool> AddAsync(Item item, string userName)
{
    // ✅ USE VALIDATION HELPER
    var validation = ValidationHelper.ValidateItem(item);
    if (!validation.IsValid)
    {
        _logger?.LogWarning("Invalid item data: {Error}", validation.Error);
        return false;
    }

    await unitOfWork.Items.AddAsync(item);
    await unitOfWork.SaveChangesAsync();

    await LogAuditAsync(item.Id, AuditActionType.Add, JsonSerializer.Serialize(item), userName);

    await notificationService?.NotifyEntityChangedAsync<Item>(
        item.Id, "Add", new { item.Name, item.Stock });

    return true;
}
```

### Usage in Blazor Form (for consistency)
```razor
<!-- VetManagement.Staff.UI/Components/Forms/ItemForm.razor -->
@using VetManagement.Staff.UI.Helpers

<MudForm @ref="mudForm" @bind-IsValid="@IsValid">
    <!-- Form fields... -->
</MudForm>

@code {
    private async Task HandleValidSubmit()
    {
        // ✅ SERVER & CLIENT VALIDATE SAME RULES
        var validation = ValidationHelper.ValidateItem(ItemModel);
        if (!validation.IsValid)
        {
            Snackbar.Add($"Validation failed: {validation.Error}", Severity.Error);
            return;
        }

        // Proceed with submission...
    }
}
```

---

## Summary Table: Which Fix Applies Where?

| Fix | Files Changed | Impact | Dependency |
|-----|-------|--------|----------|
| Fix #1: AuditService Repository | AuditLogRepository, AuditService, Controller | N+1 queries fixed | Independent |
| Fix #2: StockAlertFilter | InventoryEnums, ItemRepository, ItemsController | Type safety | Independent |
| Fix #3: ItemSortField | InventoryEnums, ItemRepository | UX improvement | Depends on Fix #2 |
| Fix #4: Enum Mapping | AuditActionHelper, InventoryMovementService | DRY principle | Independent |
| Fix #5: ValidationHelper | ValidationHelper (new), ItemService, ItemForm | Consistency | Independent |

**Recommended Order**:
1. **Fix #1 & #2** together (critical for performance)
2. **Fix #3** (quick, improves UX)
3. **Fix #4** (best practices)
4. **Fix #5** (consistency, helps prevent bugs)

