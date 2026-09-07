# VetManagement - Implementation Checklist & Action Items

**Created**: January 2025  
**Target**: Ensure scalability before adding Client/CRM features

---

## Phase 1: CRITICAL FIXES (Week 1 - ~8-10 hours)

### Task 1.1: Refactor AuditService Filtering to Repository
**Status**: ⬜ Pending  
**Effort**: 3 hours  
**Files Affected**:
- `VetManagement.Infrastructure/Repositories/AuditLogRepository.cs` (modify)
- `VetManagement.Application/Services/AuditService.cs` (simplify)

**Checklist**:
- [ ] Add `GetFilteredPagedAsync()` method to `AuditLogRepository`
- [ ] Move all filter logic (entityId, entityName, action, user, date range) to repository
- [ ] Return `(List<AuditLog>, int)` tuple with total count
- [ ] Update `AuditService.GetLogsPagedAsync()` to call repository method
- [ ] Remove `GetLogsFilteredAsync()` method (deprecated)
- [ ] Test paging works for 1K+ audit logs

**Acceptance Criteria**:
✅ Audit logs load in < 500ms even with 100K rows  
✅ Only filtered rows transmitted over network  
✅ Database indexes utilized (check SQL query plan)

---

### Task 1.2: Refactor InventoryMovementService Filtering to Repository
**Status**: ⬜ Pending  
**Effort**: 3 hours  
**Files Affected**:
- `VetManagement.Infrastructure/Repositories/InventoryMovementRepository.cs` (modify)
- `VetManagement.Application/Services/InventoryMovementService.cs` (simplify)

**Checklist**:
- [ ] Add `GetFilteredPagedAsync()` method to repository
- [ ] Move all filter logic (itemName, type, responsible, date range) to repository
- [ ] Return `(List<InventoryMovement>, int)` tuple
- [ ] Update service to call repository method
- [ ] Remove `GetMovementsFilteredAsync()` method
- [ ] Test paging works with multiple filters active

**Acceptance Criteria**:
✅ Movements load in < 500ms even with 500K rows  
✅ Multiple filters work together correctly  
✅ Date range filtering works server-side

---

### Task 1.3: Create StockAlertFilter Enum
**Status**: ⬜ Pending  
**Effort**: 1 hour  
**Files Affected**:
- `VetManagement.Shared/Enums/InventoryEnums.cs` (add new enum)
- `VetManagement.Infrastructure/Repositories/ItemRepository.cs` (update parameter)
- `VetManagement.Api/Controllers/Inventory/ItemsController.cs` (update call)

**Checklist**:
- [ ] Create `StockAlertFilter` enum with values: None, LowStock, OutOfStock
- [ ] Add `[DisplayString]` attribute to each enum value
- [ ] Update `ItemRepository.GetPagedListAsync()` signature to use `StockAlertFilter?` instead of `string? alert`
- [ ] Update repository method logic to use enum switch
- [ ] Update `ItemsController` to pass enum (ASP.NET auto-parses)
- [ ] Update Blazor components to use enum instead of string

**Acceptance Criteria**:
✅ Type-safe filtering (compile-time errors for typos)  
✅ Blazor IntelliSense shows enum values  
✅ Old string-based filtering removed

---

### Task 1.4: Add InventoryMovementRepository.GetFilteredPagedAsync()
**Status**: ⬜ Pending  
**Effort**: 2 hours  
**Files Affected**:
- `VetManagement.Infrastructure/Repositories/InventoryMovementRepository.cs` (add method)

**Checklist**:
- [ ] Add method signature with page, pageSize, and all filter parameters
- [ ] Include ItemId, MovementType, DateRange, ResponsibleUser filters
- [ ] Apply `.AsNoTracking()` for read-only query
- [ ] Sort by Date descending
- [ ] Apply pagination last
- [ ] Return (Items, TotalCount) tuple
- [ ] Test with various filter combinations

**Acceptance Criteria**:
✅ Works with 0 filters (all movements paged)  
✅ Works with multiple active filters  
✅ Pagination respects filter total count  
✅ Performance > 1000 movements/page < 200ms

---

## Phase 2: HIGH-PRIORITY IMPROVEMENTS (Week 2 - ~6-8 hours)

### Task 2.1: Centralize InventoryMovementType → AuditActionType Mapping
**Status**: ⬜ Pending  
**Effort**: 1 hour  
**Files Affected**:
- `VetManagement.Shared/Helpers/AuditActionHelper.cs` (add method)
- `VetManagement.Application/Services/InventoryMovementService.cs` (update)
- Any other services using this mapping

**Checklist**:
- [ ] Add `public static AuditActionType? MapMovementTypeToAudit(InventoryMovementType type)` to AuditActionHelper
- [ ] Implement switch expression with all 6 movement types
- [ ] Handle edge case (None → null)
- [ ] Update InventoryMovementService.AddMovementAsync() to call helper
- [ ] Search codebase for duplicate mappings and consolidate
- [ ] Add unit tests for all 7 cases

**Acceptance Criteria**:
✅ Single source of truth for mapping  
✅ All enum values covered (no missing cases)  
✅ Null case handled correctly

---

### Task 2.2: Complete ItemSortField Enum
**Status**: ⬜ Pending  
**Effort**: 30 minutes  
**Files Affected**:
- `VetManagement.Shared/Enums/InventoryEnums.cs` (add fields)

**Checklist**:
- [ ] Add `Id` to ItemSortField
- [ ] Add `LowStockThreshold` to ItemSortField
- [ ] Add `BuyPrice` to ItemSortField
- [ ] Verify all new fields are handled in `ItemRepository.GetPagedListAsync()` switch
- [ ] Test sorting by each field in ItemsController

**Acceptance Criteria**:
✅ All item properties can be sorted  
✅ UI components can bind to new sort fields  
✅ API returns correctly sorted results

---

### Task 2.3: Update ItemRepository.GetPagedListAsync() with Enum Filtering
**Status**: ⬜ Pending  
**Effort**: 2 hours  
**Files Affected**:
- `VetManagement.Infrastructure/Repositories/ItemRepository.cs` (refactor)

**Checklist**:
- [ ] Update method signature to accept `StockAlertFilter?` instead of `string? alert`
- [ ] Remove string-based alert logic
- [ ] Implement enum-based switch for alert filtering
- [ ] Test all alert filter combinations
- [ ] Verify sorting with all ItemSortField values
- [ ] Test pagination + sorting + filtering together

**Acceptance Criteria**:
✅ All 3 alert filters work (None, LowStock, OutOfStock)  
✅ Sorting works on all 10 fields  
✅ Combined filters don't conflict  
✅ Performance acceptable with 10K items

---

### Task 2.4: Create ValidationHelper Class
**Status**: ⬜ Pending  
**Effort**: 3 hours  
**Files Affected**:
- Create: `VetManagement.Shared/Helpers/ValidationHelper.cs` (new file)
- `VetManagement.Application/Services/ItemService.cs` (use helper)
- `VetManagement.Shared/Components/Forms/ItemForm.razor` (reference)

**Checklist**:
- [ ] Create `public static class ValidationHelper` in correct location
- [ ] Add `(bool IsValid, string? Error) ValidateItem(Item item)` method
  - Check Name not empty
  - Check Type not None
  - Check Stock >= 0
  - Check SellPrice >= 0
  - Check BuyPrice >= 0
  - Check Barcode not empty
  - Check Barcode unique (async overload)
- [ ] Add overload for Exam validation
- [ ] Add overload for Stock threshold validation
- [ ] Update ItemService.AddAsync() to use ValidationHelper
- [ ] Ensure server-side matches client-side validation rules

**Acceptance Criteria**:
✅ All validation rules in one place  
✅ Server & client validation identical  
✅ Error messages user-friendly  
✅ Complex validations (e.g., Barcode uniqueness) async-capable

---

## Phase 3: MEDIUM-PRIORITY REFACTORING (Week 3 - ~5-6 hours)

### Task 3.1: Reorganize Helpers Folder Structure
**Status**: ⬜ Pending  
**Effort**: 2 hours  
**Files Affected**:
- `VetManagement.Shared/Helpers/` (restructure)

**Checklist**:
- [ ] Create folder structure:
  ```
  VetManagement.Shared/Helpers/
  ├── Domain/
  │   ├── AuditActionHelper.cs       (move & expand)
  │   ├── InventoryHelper.cs         (new - inventory logic)
  │   └── ValidationHelper.cs        (move here)
  ├── UI/
  │   ├── InventoryUiHelpers.cs      (already here)
  │   └── FormHelpers.cs             (new - form defaults)
  └── Extensions/
      ├── StringExtensions.cs        (move here)
      ├── CollectionExtensions.cs    (move here)
      └── EnumExtensions.cs          (create if needed)
  ```
- [ ] Move files to new structure
- [ ] Update all `using` statements in codebase
- [ ] Ensure no circular dependencies
- [ ] Test project builds

**Acceptance Criteria**:
✅ Clear separation: Domain vs UI vs Extensions  
✅ All imports updated  
✅ No build errors  
✅ Future developers understand helper categories

---

### Task 3.2: Remove CollectionHelper from Application Layer
**Status**: ⬜ Pending  
**Effort**: 1 hour  
**Files Affected**:
- `VetManagement.Application/Helpers/CollectionHelper.cs` (move to Shared)
- Update imports in Application

**Checklist**:
- [ ] Review `CollectionHelper` functionality
- [ ] Move to `VetManagement.Shared/Helpers/Extensions/CollectionExtensions.cs`
- [ ] Update all imports in Application layer
- [ ] Delete original file
- [ ] Test Application builds

**Acceptance Criteria**:
✅ No helpers in Application layer  
✅ All functionality preserved  
✅ Correct layering maintained

---

### Task 3.3: Create Standardized Query Guidelines Document
**Status**: ⬜ Pending  
**Effort**: 2 hours  
**Files Affected**:
- Create: `docs/guidelines/QUERY_PATTERNS.md` (new file)

**Checklist**:
- [ ] Document standard repository method signatures
- [ ] Show pagination pattern with code examples
- [ ] Show filtering pattern with code examples
- [ ] Explain when to use `.AsNoTracking()`
- [ ] Explain when to use `.Include()` for relationships
- [ ] Explain when to use switch expressions vs if-else
- [ ] Provide service delegation pattern
- [ ] Add checklist for code review

**Acceptance Criteria**:
✅ New team members can follow pattern  
✅ Code review uses document as reference  
✅ Examples cover all common scenarios

---

## Phase 4: PREPARATION FOR CLIENTS FEATURE (Week 4+ - Planning)

### Task 4.1: Create ClientRepository Template
**Status**: ⬜ Pending  
**Effort**: Planning only (1 hour)  
**Preview**:
```csharp
public class ClientRepository(AppDbContext context) : Repository<Client>(context), IClientRepository
{
    // Query Methods
    public async Task<Client?> GetByTaxIdAsync(string taxId);
    public async Task<(List<Client> Items, int TotalCount)> GetFilteredPagedAsync(
        int page, int pageSize,
        string? searchTerm, // Name or TaxId
        string? city,
        DateTime? registeredFrom,
        ClientSortField? sortBy);

    // Command Methods
    public override async Task AddAsync(Client client);
    public override async Task UpdateAsync(Client client);
}
```

**Checklist**:
- [ ] Plan ClientSortField enum
- [ ] Plan ClientValidationHelper
- [ ] Plan relationship loading (.Include(c => c.Pets))

---

### Task 4.2: Create Query Optimization Checklist
**Status**: ⬜ Pending  
**Effort**: 1 hour  

**Checklist Before Committing Any Repository Query**:
- [ ] Uses `.AsNoTracking()` for read-only queries
- [ ] Uses `.Include()` or `.ThenInclude()` for relationships
- [ ] Pagination applied LAST in query chain
- [ ] No magic strings (use enums)
- [ ] FilteredPagedAsync pattern implemented
- [ ] No `GetAllAsync()` in Service layer (use filtered methods)
- [ ] Performance tested (< 500ms for standard page)
- [ ] Database indexes considered for filter fields

---

## Phase 5: TESTING REQUIREMENTS

### Unit Tests to Add
- [ ] `AuditActionHelperTests.MapMovementTypeToAudit()` - 7 cases
- [ ] `ValidationHelperTests.ValidateItem()` - valid, all invalid cases
- [ ] `ItemRepositoryTests.GetPagedListAsync()` - with all combinations of filters
- [ ] `InventoryMovementRepositoryTests.GetFilteredPagedAsync()` - pagination, filters, sorts

### Integration Tests to Add
- [ ] `AuditServiceTests.GetLogsPagedAsync()` - end-to-end with API
- [ ] `ItemsControllerTests.GetPagedAsync()` - with sorting, filtering, paging
- [ ] `InventoryMovementControllerTests.GetFilteredAsync()` - similar

---

## Summary Table: Quick Reference

| Task | Hours | Files | Priority | Week |
|------|-------|-------|----------|------|
| 1.1: AuditService filtering | 3 | 2 | 🔴 Critical | W1 |
| 1.2: InventoryMovement filtering | 3 | 2 | 🔴 Critical | W1 |
| 1.3: StockAlertFilter enum | 1 | 3 | 🔴 Critical | W1 |
| 1.4: InventoryMovement pagination | 2 | 1 | 🔴 Critical | W1 |
| 2.1: Centralize enum mappings | 1 | 2 | 🟡 High | W2 |
| 2.2: Complete ItemSortField | 0.5 | 1 | 🟡 High | W2 |
| 2.3: Update filtering enum | 2 | 1 | 🟡 High | W2 |
| 2.4: ValidationHelper | 3 | 3 | 🟡 High | W2 |
| 3.1: Reorganize helpers | 2 | 8 | 🟢 Medium | W3 |
| 3.2: Remove App layer helpers | 1 | 2 | 🟢 Medium | W3 |
| 3.3: Query guidelines doc | 2 | 1 | 🟢 Medium | W3 |
| **TOTAL** | **~23 hours** | **~35 files** | - | **3 weeks** |

---

## How to Use This Checklist

1. **Print or bookmark** this file for reference
2. **Mark off tasks** as you complete them (replace ⬜ with ✅)
3. **Link to code review** comments with specific task numbers
4. **Before starting Clients feature**, ensure all Phase 1+2 complete
5. **Update this file** as you discover new patterns to enforce

---

## Questions to Ask Before Starting

❓ Do you want to do all Priority 1 tasks before starting on Priority 2?  
❓ Should we create a branch for this refactoring work?  
❓ Do you want me to provide code snippets for each task?  
❓ Should we add unit tests as we go, or after refactoring?  

---

**Last Updated**: January 2025  
**Status**: Ready for Phase 1 implementation
