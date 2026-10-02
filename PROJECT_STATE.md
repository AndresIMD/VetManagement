# VetManagement - Project State Tracker

**Last Updated:** 2026-10-02  
**Current Branch:** main  

**Target dependency direction:** Domain ← Application ← Infrastructure ← Api; Contracts is consumed by Api and all clients.
Solution folders: Core (Domain, Application, Contracts) · Infrastructure · Server (Api) · Clients (Shared, Staff.Web, Staff.Maui) · Clinic Sites (sites/SPVetClinic) · Tests.
Product model: the staff CRM is generic (same for every clinic); each clinic gets its own branded public site that consumes the API. Clinic sites do not use the `VetManagement.` prefix.
Pending rename: `VetManagement.Shared` → `VetManagement.Staff.UI` after F2 (avoids touching backend usings that F2 removes).
Backend projects (Application, Infrastructure, Api) must stop referencing Shared — F2 moves Client, Pet, Exams, MedicalVisit, AuditLog and enums from `Shared/Models` to Domain.

---

## Phase Progress Summary

| Phase | Status | Description |
|-------|--------|-------------|
| **F0** | ✅ **COMPLETED** | Restructuring + Critical fixes + CI |
| **F1** | ✅ **COMPLETED** | Inventory migration to Domain/Contracts |
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

## F1 - Inventory Migration (Completed)

### Sub-phase Status

| Step | Task | Status | Notes |
|------|------|--------|-------|
| F1.1 | Domain inventory types | ✅ Done | Item, Drug, DosageRange, InventoryMovement, enums |
| F1.2 | Contracts transport DTOs | ✅ Done | InventoryItemDto, ItemCreateRequest, ItemUpdateRequest, etc. |
| F1.3 | **AppDbContext + Repos → Domain** | ✅ Done | AppDbContext, ItemRepository, InventoryMovementRepository use Domain inventory entities |
| F1.3b | IItemRepository interface | ✅ Done | Inventory repository contracts use Domain types |
| F1.3c | Application Services (ItemService, etc.) | ✅ Done | Inventory services use Domain entities and enums |
| F1.4 | Controllers → Contracts DTOs | ✅ Done | Inventory controllers use request/response contracts and explicit mappings |
| F1.5 | Shared UI + API clients → DTOs | ✅ Done (decision) | API clients talk Contracts over HTTP. `Shared` Item/Drug/InventoryMovement are kept as **UI view models** (form validation, `Drug : Item`); mapping lives only in `ItemsApiService` / `InventoryMovementApiService`. Backend no longer uses them. |
| F1.6 | Tests update | ✅ Done | Existing inventory unit and integration tests use Domain types |

---

## Current Blockers / Decisions Needed

1. **ExamPerformed** - Has `List<ExamRequestItem>` in Shared. `ExamRequestItem` doesn't reference Inventory types directly, but existing EF migrations contain legacy inventory type names.
2. **Legacy migrations** - Existing EF migration snapshots reference `Shared.Models.Core.Item`; validate the production database migration strategy before generating new migrations.

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

**F2 — move remaining entities out of Shared** (one module per commit; build + tests after each):
1. Review legacy EF migrations / model snapshot: confirm moving CLR types between namespaces produces an empty migration (table names, `Item` discriminator values).
2. Shared enums used by the backend → `Domain.Enums`.
3. Clients/Pets: `Client`, `Pet` → Domain; API exposes Contracts DTOs; Shared keeps UI view models + ApiService mapping (same pattern as inventory).
4. Exams: `Exam`, `ExamPerformed`, `ExamRequestItem`, `ExternalLab` → Domain.
5. Medical: `MedicalVisit` → Domain. Audit: `AuditLog` → Domain.
6. Backend DTOs still in `Shared/Models/DTOs` (accounts, users, paging, mass update) → Contracts.
7. Remove the `Shared` ProjectReference from Application, Infrastructure and Api.
8. Rename `VetManagement.Shared` → `VetManagement.Staff.UI`.

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