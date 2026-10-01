# VetManagement - Project State Tracker

**Last Updated:** 2026-09-08  
**Current Branch:** main  
**HEAD:** 33d8246 (F1 additive)

---

## Phase Progress Summary

| Phase | Status | Description |
|-------|--------|-------------|
| **F0** | ✅ **COMPLETED** | Restructuring + Critical fixes + CI |
| **F1** | 🟡 **IN PROGRESS** | Inventory migration to Domain/Contracts |
| **F2** | ⏳ Pending | Other modules migration |
| **F3** | ⏳ Pending | Shared → UI only |
| **F4** | ⏳ Pending | Hardening |
| **F5** | ⏳ Pending | Documentation |

---

## F0 - Completed (6 commits)

| Commit | SHA | Description |
|--------|-----|-------------|
| 1 | `746864f` | Restructure: move projects to `src/`, `tests/`, `docs/` |
| 2 | `3d4c20b` | **C1**: Fix policy mismatch (Inventory.READ→Inventory.Read, etc.) + AuthorizationPoliciesTests |
| 3 | `fa3a2c8` | **C2**: Remove non-existent rate limiter policy from SetupController |
| 4 | `ded4abc` | Cleanup: csproj.new, empty folders, .http (SPLabHybrid→VetManagement), README |
| 5 | `5d009ca` / `e0f4359` | **CI**: .github/workflows/ci.yml + fix .gitignore for workflows |
| 6 | `33d8246` | **F1 additive**: Domain inventory types + Contracts DTOs |

---

## F1 - Inventory Migration (Current Phase)

### Sub-phase Status

| Step | Task | Status | Notes |
|------|------|--------|-------|
| F1.1 | Domain inventory types | ✅ Done | Item, Drug, DosageRange, InventoryMovement, enums |
| F1.2 | Contracts transport DTOs | ✅ Done | InventoryItemDto, ItemCreateRequest, ItemUpdateRequest, etc. |
| F1.3 | **AppDbContext + Repos → Domain** | ✅ Done | AppDbContext, ItemRepository, InventoryMovementRepository use Domain inventory entities |
| F1.3b | IItemRepository interface | ✅ Done | Inventory repository contracts use Domain types |
| F1.3c | Application Services (ItemService, etc.) | ✅ Done | Inventory services use Domain entities and enums |
| F1.4 | Controllers → Contracts DTOs | ✅ Done | Inventory controllers use request/response contracts and explicit mappings |
| F1.5 | Shared UI + API clients → DTOs | 🟡 **In Progress** | Items and movement API clients use Contracts over HTTP; UI models remain transitional adapters |
| F1.6 | Tests update | ✅ Done | Existing inventory unit and integration tests use Domain types |

---

## Current Blockers / Decisions Needed

1. **Shared UI models** - Inventory pages and components still use legacy `Shared.Models.Core.Item`, `Shared.Models.Inventory.InventoryMovement`, and shared DTOs through adapters.
2. **ExamPerformed** - Has `List<ExamRequestItem>` in Shared. `ExamRequestItem` doesn't reference Inventory types directly, but existing EF migrations contain legacy inventory type names.
3. **Legacy migrations** - Existing EF migration snapshots reference `Shared.Models.Core.Item`; validate the production database migration strategy before generating new migrations.

---

## Commands for Quick State Verification

```bash
# Build all
dotnet build VetManagement.sln --no-restore

# Test all
dotnet test tests/VetManagement.Tests/VetManagement.Tests.csproj --no-restore --verbosity minimal

# Git status
git status --short -b

# Recent commits
git log --oneline -10
```

---

## Next Actions (Priority Order)

1. **Migrate Shared inventory UI models** from legacy entities to `Contracts` DTOs, starting with pages and reusable inventory modals.
2. Remove the temporary model adapters from `ItemsApiService` and `InventoryMovementApiService` once UI consumers use contracts.
3. Add focused API/client tests for request and response mappings.
4. Review legacy EF migrations and establish the migration boundary for Domain inventory entities.
5. Run build and tests after each UI migration slice.
6. Commit F1 only after the Shared UI no longer depends on legacy inventory entities.

---

## Git History (Last 10)

```
33d8246 F1 (additive): add Domain inventory types + Contracts transport DTOs
e0f4359 Add CI workflow for build and test on push/PR
ae4d548 Add CI workflow for build and test on push/PR
5d009ca Add CI workflow and fix xUnit1031 async warning in policy tests
ded4abc Cleanup: remove legacy artifacts and update docs to src/ layout
fa3a2c8 Fix C2: remove non-existent 'fixed-window' rate limiter policy from SetupController
3d4c20b Fix C1: normalize authorization policy names to PascalCase in controllers
746864f Restructure: move projects to src/, tests/, docs/
ed8175d Initial commit: VetManagement - Full-stack veterinary clinic CRM
```

---

*This file is auto-updated manually after each significant change. Keep it in repo root for continuity across sessions.*