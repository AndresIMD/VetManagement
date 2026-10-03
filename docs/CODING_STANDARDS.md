# VetManagement Coding Standards & Patterns

**Purpose**: Single source of truth for coding style, architectural patterns, and best practices in VetManagement project.

**Audience**: Developers and AI assistants working on this codebase.

**Scope**: C# 14, .NET 10, Clean Architecture, Blazor WebAssembly, ASP.NET Core.

---

## 1. C# 14 Language Features (MANDATORY)

### 1.1 Primary Constructors (ALWAYS USE)

✅ **DO**:
```csharp
public class ItemService(IUnitOfWork unitOfWork, ILogger<ItemService> logger)
{
    public async Task UpdateAsync(Item item) => await unitOfWork.Items.UpdateAsync(item);
}
```

❌ **DON'T** (redundant fields):
```csharp
public class ItemService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    public ItemService(IUnitOfWork unitOfWork) { ... }
}
```

**Rationale**: Primary constructors eliminate boilerplate; parameters are implicitly accessible throughout the class.

---

### 1.2 Null-Conditional & Null-Coalescing Operators

✅ **DO**:
```csharp
await notificationService?.NotifyAsync(entity);
string name = user?.FullName ?? "Unknown";
```

❌ **DON'T**:
```csharp
if (notificationService != null) { await notificationService.NotifyAsync(entity); }
string name = user != null ? user.FullName : "Unknown";
```

---

### 1.3 Target-Typed New Expression

✅ **DO**:
```csharp
List<Item> items = [];
Item item = new() { Name = "Vaccine", Price = 50m };
return new() { Items = items, TotalCount = 10 };
```

❌ **DON'T**:
```csharp
List<Item> items = new List<Item>();
Item item = new Item { Name = "Vaccine", Price = 50m };
return new PagedResult<Item> { Items = items, TotalCount = 10 };
```

---

### 1.4 Collection Expressions & Ranges

✅ **DO**:
```csharp
int[] ids = [1, 2, 3, 4, 5];
var sublist = items[1..3];  // items[1] and items[2]
```

❌ **DON'T**:
```csharp
int[] ids = new int[] { 1, 2, 3, 4, 5 };
var sublist = items.Skip(1).Take(2).ToList();
```

---

### 1.5 File-Scoped Namespaces (ALWAYS USE)

✅ **DO**:
```csharp
namespace VetManagement.Application.Services;

public class ItemService(IUnitOfWork unitOfWork) { ... }
```

❌ **DON'T**:
```csharp
namespace VetManagement.Application.Services
{
    public class ItemService(IUnitOfWork unitOfWork) { ... }
}
```

---

### 1.6 Switch Expressions

✅ **DO**:
```csharp
string description = movementType switch
{
    InventoryMovementType.Ingress => "Stock In",
    InventoryMovementType.Egress => "Stock Out",
    _ => "Unknown"
};
```

❌ **DON'T**:
```csharp
string description;
switch (movementType)
{
    case InventoryMovementType.Ingress:
        description = "Stock In";
        break;
    case InventoryMovementType.Egress:
        description = "Stock Out";
        break;
    default:
        description = "Unknown";
        break;
}
```

---

## 2. Code Style & Comments

### 2.1 Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Private fields | `_camelCase` | `_itemRepository` |
| Local variables | `camelCase` | `itemId`, `userName` |
| Public properties | `PascalCase` | `TotalCount`, `ItemName` |
| Public methods | `PascalCase` | `GetByIdAsync()`, `UpdateAsync()` |
| Async methods | Always end with `Async` | `GetAsync()`, `SaveChangesAsync()` |
| Constants | `UPPER_SNAKE_CASE` | `MAX_RETRY_COUNT`, `DEFAULT_TIMEOUT_MS` |
| Enums | `PascalCase` (values too) | `InventoryMovementType`, `Ingress`, `Egress` |

### 2.2 International Naming (CRITICAL)

**ALWAYS use English + international terms** (country-specific *validation* may live in a value type, e.g.
`Domain/Clients/Rut.cs`, but the stored field is still `TaxId`):

✅ **DO**:
```csharp
public class Person
{
    public string TaxId { get; set; }  // International: not Rut, NIF, etc.
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
}
```

❌ **DON'T**:
```csharp
public class Person
{
    public string Rut { get; set; }  // Spanish/Chilean specific
    public string Direccion { get; set; }  // Spanish
    public string Telefono { get; set; }  // Spanish
}
```

---

### 2.3 Comments (PROFESSIONAL & MINIMAL)

**Philosophy**: Code should be self-documenting. Comments explain *why*, not *what*.

✅ **DO** - Explain reasoning:
```csharp
// Use AsNoTracking for audit reads; EF will reattach during update
var oldData = await repository.GetByIdAsNoTrackingAsync(id);
```

✅ **DO** - XML docs for public APIs:
```csharp
/// <summary>
/// Retrieves inventory movements with optional filtering and server-side paging.
/// </summary>
/// <param name="page">Zero-based page number (default: 0)</param>
/// <param name="pageSize">Items per page (default: 25)</param>
public async Task<(List<InventoryMovement> Items, int TotalCount)> GetFilteredPagedAsync(
    int page = 0, int pageSize = 25)
{
    // ...
}
```

❌ **DON'T** - Redundant comments:
```csharp
// Loop through items
foreach (var item in items) { ... }

// TODO: Fix this later
// HACK: This is a temporary solution
// Updated by AI on 2025-01-15
```

❌ **DON'T** - Tutorial/chatty comments:
```csharp
// This service is responsible for handling items
// We use async/await here because it's non-blocking
// This pattern is good practice in modern .NET
```

---

## 3. Architecture Patterns

### 3.1 Repository + Unit of Work Pattern

**Repositories**: Data access + filtering, delegated to Infrastructure layer.

```csharp
// VetManagement.Infrastructure/Repositories/ItemRepository.cs
public async Task<(List<Item> Items, int TotalCount)> GetFilteredPagedAsync(
    int page = 0, int pageSize = 25, ItemSortField? sortBy = null, SortDirection sortDirection = SortDirection.Ascending)
{
    IQueryable<Item> query = dbContext.Items;

    // Apply sorting
    query = sortBy switch
    {
        ItemSortField.Name => sortDirection == SortDirection.Ascending 
            ? query.OrderBy(i => i.Name)
            : query.OrderByDescending(i => i.Name),
        _ => query.OrderBy(i => i.Id)
    };

    int totalCount = await query.CountAsync();
    var items = await query.Skip(page * pageSize).Take(pageSize).ToListAsync();
    return (items, totalCount);
}
```

**Unit of Work**: Coordinates repositories and savepoints (Application layer).

```csharp
// VetManagement.Application/Services/ItemService.cs
public class ItemService(IUnitOfWork unitOfWork)
{
    public async Task UpdateAsync(Item item, Item oldData, string userName)
    {
        // 1. Database operation
        await unitOfWork.Items.UpdateAsync(item);
        await unitOfWork.SaveChangesAsync();

        // 2. Log audit
        await unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            Action = AuditActionType.Edit,
            EntityName = nameof(Item),
            EntityId = item.Id,
            UserName = userName,
            OldData = JsonConvert.SerializeObject(oldData),
            NewData = JsonConvert.SerializeObject(item)
        });

        // 3. Send notification
        await notificationService?.NotifyEntityChangedAsync(item, AuditActionType.Edit);
    }
}
```

**Key Pattern**: Save → Log → Notify (all within service method).

---

### 3.2 Server-Side Pagination Repository Pattern

✅ **DO** - Delegate filtering to DB:

```csharp
public async Task<(List<AuditLog> Items, int TotalCount)> GetFilteredPagedAsync(
    int page = 0, int pageSize = 25, int? entityId = null, 
    string? entityName = null, AuditActionType? action = null, 
    string? user = null, DateTime? from = null, DateTime? to = null)
{
    var query = dbContext.AuditLogs.AsQueryable();

    if (entityId.HasValue) query = query.Where(x => x.EntityId == entityId);
    if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(x => x.EntityName == entityName);
    if (action.HasValue) query = query.Where(x => x.Action == action);
    if (!string.IsNullOrWhiteSpace(user)) query = query.Where(x => x.UserName == user);
    if (from.HasValue) query = query.Where(x => x.Date >= from);
    if (to.HasValue) query = query.Where(x => x.Date <= to);

    int totalCount = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.Date)
        .Skip(page * pageSize).Take(pageSize).ToListAsync();

    return (items, totalCount);
}
```

❌ **DON'T** - Load all & filter in-memory:
```csharp
public async Task<List<AuditLog>> GetFilteredAsync(int? entityId = null, ...)
{
    var allLogs = await dbContext.AuditLogs.ToListAsync();  // ❌ LOADS EVERYTHING
    return allLogs
        .Where(x => entityId == null || x.EntityId == entityId)
        .Where(x => action == null || x.Action == action)
        .Skip(page * pageSize).Take(pageSize).ToList();
}
```

---

### 3.3 EF Core Tracking Rules (CRITICAL)

**RULE**: When client sends entity via JSON (PUT/PATCH), use `GetByIdAsNoTrackingAsync()` for read-only audit.

❌ **WRONG** - Causes tracking conflicts:
```csharp
[HttpPut("{id}")]
public async Task<IActionResult> UpdateAsync(int id, [FromBody] Item item)
{
    var oldData = await itemService.GetByIdAsync(id);  // ❌ EF tracks this
    await itemService.UpdateAsync(item, oldData, GetUserName());  // CONFLICT!
    return Ok();
}
```

✅ **CORRECT** - Use AsNoTracking for audit:
```csharp
[HttpPut("{id}")]
public async Task<IActionResult> UpdateAsync(int id, [FromBody] Item item)
{
    var oldData = await itemService.GetByIdAsNoTrackingAsync(id);  // ✅ No tracking
    await itemService.UpdateAsync(item, oldData, GetUserName());
    return Ok();
}
```

**When to use each method**:
- `GetByIdAsync()`: Modifying entity in same context, or for Delete operations
- `GetByIdAsNoTrackingAsync()`: Audit/logs, read-only queries, entity from client

---

## 4. Enum Patterns (CRITICAL FOR TYPE SAFETY)

### 4.1 Domain-Specific Enums

**RULE**: Never mix unrelated concepts in one enum.

✅ **CORRECT** - Separated by domain:
```csharp
// Stock movements only
public enum InventoryMovementType
{
    None = 0,
    Ingress = 1,
    Egress = 2,
    Adjustment = 3,
    StockUpdate = 4,
    MassiveStockIngress = 5,
    MassiveStockEgress = 6
}

// Unified audit actions with numeric ranges
public enum AuditActionType
{
    None = 0,
    // CRUD: 0-99
    Add = 1,
    Edit = 2,
    Delete = 3,
    // Inventory: 100-199
    Ingress = 100,
    Egress = 101,
    Adjustment = 102,
    StockUpdate = 103,
    // Security: 200-299
    UserLogin = 200,
    UserLoginFailed = 201,
    PasswordChanged = 202
}
```

❌ **WRONG** - Mixed concerns:
```csharp
public enum InventoryMovementType
{
    Ingress, Egress, Adjustment,  // Stock operations
    Add, Edit, Delete,            // CRUD
    UserLogin, PasswordChanged    // Security
}
```

---

### 4.2 Enum Filtering Helper Pattern

Centralize enum-to-enum mappings:

```csharp
// VetManagement.Staff.UI/Helpers/AuditActionHelper.cs
public static class AuditActionHelper
{
    /// <summary>
    /// Convert domain-specific InventoryMovementType to unified AuditActionType.
    /// </summary>
    public static AuditActionType ToAuditActionType(this InventoryMovementType movementType)
        => movementType switch
        {
            InventoryMovementType.Ingress => AuditActionType.Ingress,
            InventoryMovementType.Egress => AuditActionType.Egress,
            InventoryMovementType.Adjustment => AuditActionType.Adjustment,
            InventoryMovementType.StockUpdate => AuditActionType.StockUpdate,
            InventoryMovementType.MassiveStockIngress => AuditActionType.Ingress,
            InventoryMovementType.MassiveStockEgress => AuditActionType.Egress,
            _ => AuditActionType.None
        };

    /// <summary>
    /// Get all inventory-related audit actions.
    /// </summary>
    public static IEnumerable<AuditActionType> GetInventoryActions()
        => [AuditActionType.Ingress, AuditActionType.Egress, AuditActionType.Adjustment, AuditActionType.StockUpdate];
}
```

**Usage**:
```csharp
var auditAction = movement.Type.ToAuditActionType();
var inventoryLogs = logs.Where(x => AuditActionHelper.GetInventoryActions().Contains(x.Action));
```

---

### 4.3 DisplayString Attribute

All enums must have `[DisplayString]` for UI rendering:

```csharp
public enum InventoryMovementType
{
    [DisplayString("None")]
    None = 0,

    [DisplayString("Stock Ingress")]
    Ingress = 1,

    [DisplayString("Stock Egress")]
    Egress = 2
}
```

**Usage in Blazor**:
```razor
@foreach (var type in Enum.GetValues<InventoryMovementType>())
{
    <MudSelectItem Value="@type">@type.DisplayString()</MudSelectItem>
}
```

---

## 5. Async/Await & Task Patterns

### 5.1 Always Async for I/O Operations

✅ **DO**:
```csharp
public async Task<Item?> GetByIdAsync(int id)
    => await dbContext.Items.FirstOrDefaultAsync(x => x.Id == id);

public async Task SaveAsync()
    => await dbContext.SaveChangesAsync();
```

❌ **DON'T** (blocking on async):
```csharp
public Item? GetById(int id)
    => dbContext.Items.FirstOrDefault(x => x.Id == id).Result;  // ❌ Deadlock risk

public void Save()
    => dbContext.SaveChanges();  // ❌ Synchronous DB call
```

## 6. Validation & Type Safety

### 6.1 Model Validation (DTOs)

✅ **DO** - Use data annotations:
```csharp
public class CreateItemDto
{
    [Required(ErrorMessage = "Item name is required")]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    public ItemType Type { get; set; }
}
```

✅ **DO** - Enum defaults to non-zero:
```csharp
public static Item GetEmpty() => new(
    name: string.Empty,
    type: ItemType.Material,  // NOT ItemType.None
    stock: 0,
    price: 0m
);
```

---

### 6.2 Null-Safety

✅ **DO** - Use nullable reference types:
```csharp
#nullable enable

public class Item
{
    public string Name { get; set; } = null!;  // Non-nullable, must initialize
    public string? Description { get; set; }   // Nullable
}

public async Task<Item?> GetByIdAsync(int id)  // Can return null
    => await dbContext.Items.FirstOrDefaultAsync(x => x.Id == id);
```

---

## 7. Blazor WebAssembly Specific Patterns

### 7.1 Filter Property Pattern (Reactive)

```csharp
private string _filterName = "";

private string FilterName
{
    get => _filterName;
    set
    {
        if (_filterName != value)
        {
            _filterName = value;
            _ = (_table?.ReloadServerData() ?? Task.CompletedTask);
        }
    }
}
```

**Key Points**:
- Check for actual change before triggering reload
- Use discard (`_`) for fire-and-forget
- Handle null table reference

---

### 7.2 MudTable Reactive Binding with Debouncing

```razor
<MudTextField @bind-Value="FilterItemName" 
              Label="Search..." 
              Variant="Variant.Outlined"
              Clearable="true" 
              Immediate="true" 
              DebounceInterval="400" />
```

**Critical**: `Immediate="true"` + `DebounceInterval="400"` ensures reactive updates without overwhelming the server.

---

### 7.3 Empty State Pattern

```razor
<NoRecordsContent>
    <MudStack AlignItems="AlignItems.Center" Spacing="3" Class="pa-8">
        <MudIcon Icon="@Icons.Material.Outlined.SearchOff" Size="Size.Large" Color="Color.Secondary" />
        <MudText Typo="Typo.h6" Color="Color.Secondary">No records found</MudText>
        <MudText Typo="Typo.body2" Color="Color.Tertiary" Align="Align.Center">
            @if (GetActiveFilterCount() > 0)
            {
                <text>Try adjusting your filters.</text>
            }
            else
            {
                <text>No records available yet.</text>
            }
        </MudText>
    </MudStack>
</NoRecordsContent>
```

---

## 8. File Organization

### 8.1 Enum Files

All enums live in Domain (one source of truth for backend and UI):

```
VetManagement.Domain/Enums/
├─ InventoryEnums.cs                   (Item types, movements, filters, sorting)
├─ ExamEnums.cs                        (Sample types/containers, exam item status)
├─ PetEnums.cs                         (Sex, species, reproductive status)
├─ CommonEnums.cs                      (Payments, audit actions)
├─ SchedulingEnums.cs                  (Resources, appointments, deposits, refunds)
├─ BillingEnums.cs                     (Sale status, line kinds)
├─ ClinicalEnums.cs                    (Preventive dose kind and status)
├─ EnumAttributes.cs                   (DisplayString + EnumExtensions)
└─ InventoryMovementTypeExtensions.cs  (Movement type → audit action)
```

### 8.2 Helper Files

```
VetManagement.Staff.UI/Helpers/
├─ AuditActionHelper.cs        (Enum mappings for audit)
├─ FilterHelpers.cs            (List filtering in pages)
├─ InventoryUiHelpers.cs       (UI-specific inventory logic)
├─ Money.cs                    (Chilean pesos as "$25.000"; the web client runs with invariant globalization)
├─ ObjectExtensions.cs
└─ StringExtensions.cs         (String manipulation utilities)
```

**Pattern**: Use `Extensions` suffix for extension methods, `Helpers` for static utilities.

---

## 9. Project-Wide Rules

### 9.1 No Magic Strings

❌ **DON'T**:
```csharp
if (alertType == "LowStock") { ... }
public async Task<List<Item>> GetItemsAsync(string sortBy, string sortDirection)
```

✅ **DO**:
```csharp
if (alertType == StockAlertFilter.LowStock) { ... }
public async Task<List<Item>> GetItemsAsync(ItemSortField? sortBy, SortDirection sortDirection)
```

---

### 9.2 No TODO/HACK/FIXME Comments

❌ **DON'T**:
```csharp
// TODO: Fix this later
// HACK: Temporary solution
// FIXME: This needs refactoring
```

✅ **DO**: Put pending work in `PROJECT_STATE.md` (open decisions / later). A **deliberate** simplification with a
known limit is marked with its ceiling and the upgrade path, so it is searchable and never mistaken for a bug:
```csharp
// ponytail: one clinic-wide lock; per-item keys if a clinic ever outgrows it.
```

---

### 9.3 Documentation Policy

All documentation is indexed in [`docs/README.md`](README.md).

✅ **WHERE THINGS GO**:
- XML documentation on public types and members that aren't self-explanatory; brief comments for the *why*
- `docs/architecture/ARCHITECTURE.md`: structure, rules, shared mechanisms, decisions (small text diagrams are fine)
- `docs/modules/<MODULE>.md`: one per business module — rules, per-clinic settings, permissions, limits.
  Updated in the same commit that changes the module.
- `docs/DEPLOYMENT.md`: every configuration key
- `docs/guides/`: step-by-step guides for people using the system (Spanish)
- `PROJECT_STATE.md`: where things stand (no history: that's `git log`)

❌ **FORBIDDEN**:
- README.md inside code folders (Enums, Helpers, Services, etc.)
- Status reports, reviews or plans that go stale: decide, do, and record the result in the places above
- Archived documents: delete obsolete docs (git keeps them)

---

## 10. Testing Patterns

### 10.1 xUnit + Moq + FluentAssertions

```csharp
[Fact]
public async Task UpdateAsync_WithValidItem_UpdatesSuccessfully()
{
    // Arrange
    var mockUnitOfWork = new Mock<IUnitOfWork>();
    var service = new ItemService(mockUnitOfWork.Object);
    var item = new Item { Id = 1, Name = "Vaccine" };

    // Act
    await service.UpdateAsync(item, oldItem, "admin");

    // Assert
    mockUnitOfWork.Verify(x => x.Items.UpdateAsync(item), Times.Once);
    mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
}
```

**Naming**: `MethodName_Scenario_ExpectedResult`

---

## 11. Clean Architecture Layers

The full layer map, dependency rules, module pattern and guard tests are in
[`architecture/ARCHITECTURE.md`](architecture/ARCHITECTURE.md). In short:

- **Domain**: entities and enums, no dependencies, no attributes.
- **Contracts**: request/DTO transport types with server-side validation.
- **Application**: services; depends on Domain and Contracts.
- **Infrastructure**: EF Core; implements Application's repository interfaces.
- **Api**: controllers map request → entity → DTO; never return EF entities.
- **Staff.UI**: pages, components, view models and API clients for Staff.Web and Staff.Maui.
  The backend must never reference it (`ArchitectureTests`).

---

## 12. Commit Message Guidelines

### Format

Feature work is prefixed with its phase or area (`F8.1: billing - sales, payments ...`, `Charge visit: ...`);
other commits use the types below.

```
<type>: <subject>

<body>

<footer>
```

### Types
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code restructuring (no behavior change)
- `docs`: Documentation update
- `chore`: Build, CI/CD, tooling
- `clean`: Code cleanup, remove unused code

### Examples

✅ **DO**:
```
refactor: delegate audit filtering to repository

- Move GetLogsFilteredAsync logic to AuditLogRepository.GetFilteredPagedAsync()
- Implement server-side paging with DB-side filtering
- Update AuditService to call repository method
- Reduces memory usage and improves scalability for large datasets

Related: T-010 (server-side pagination)
```

❌ **DON'T**:
```
updated stuff
fixed bugs
working on new feature
```

---

## Summary

This document defines the **architectural vision** and **coding philosophy** of VetManagement. It prioritizes:

1. **Type Safety**: Enums over strings, strong typing throughout
2. **Scalability**: Server-side paging, efficient DB queries
3. **Clarity**: Self-documenting code, minimal comments
4. **Maintainability**: Clean separation of concerns, predictable patterns
5. **Professionalism**: Consistent style, international naming, no hacks

**Golden Rule**: *"Code is read much more often than it is written. Make it readable and purposeful."*

---

**Version**: 1.1  
**Last Updated**: 2026-10-03  
**Maintainer**: VetManagement Core Team
