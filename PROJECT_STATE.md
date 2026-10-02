# VetManagement - Project State Tracker

**Last Updated:** 2026-10-02  
**Current Branch:** main  

**Target dependency direction:** Domain ← Application ← Infrastructure ← Api; Contracts is consumed by Api and all clients.
Solution folders: Core (Domain, Application, Contracts) · Infrastructure · Server (Api) · Clients (Staff.UI, Staff.Web, Staff.Maui) · Clinic Sites (sites/SPVetClinic) · Tests.
Product model: the staff CRM is generic (same for every clinic); each clinic gets its own branded public site that consumes the API. Clinic sites do not use the `VetManagement.` prefix.
Module pattern (all modules): Domain entity (no attributes) · Contracts request/DTO (server validation, same JSON shape as the UI model) · controller maps request → entity → DTO, ids only from the route · `Staff.UI` keeps its own view models (form validation, `Drug : Item`) and maps in its `*ApiService`.
Guard tests: `ArchitectureTests` (dependency direction), `MigrationsTests` (EF model = migrations snapshot), `WireContractTests` (UI ↔ API JSON), `AuthorizationPoliciesTests` (every `[Authorize(Policy)]` is registered).

---

## Phase Progress Summary

| Phase | Status | Description |
|-------|--------|-------------|
| **F0** | ✅ **COMPLETED** | Restructuring + Critical fixes + CI |
| **F1** | ✅ **COMPLETED** | Inventory migration to Domain/Contracts |
| **F2** | ✅ **COMPLETED** | Clients/Pets, Exams, Medical, Audit, enums and DTOs out of Shared; backend no longer references the UI |
| **F3** | ✅ **COMPLETED** (with F2) | Shared → UI only, renamed `VetManagement.Staff.UI` |
| **F4** | ✅ **COMPLETED** | Hardening: ProblemDetails + UI error boundary, login lockout + rate limit, DB health check, JSON logs, validation gaps closed |
| **F5** | ✅ **COMPLETED** | docs/architecture/ARCHITECTURE.md; historical docs moved to docs/archive |

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

## Notes

- **Legacy migration snapshots** still name entities by their old CLR types (`VetManagement.Shared.Models.*`). This is harmless: tables and columns are unchanged, and `MigrationsTests` fails if the model ever drifts. Applied migrations are never edited; the next new migration refreshes the snapshot names.

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

F0–F5 are complete. The next phase adds product features and needs decisions from the clinic first.

1. **F6 — Scheduling (agenda)**: the CRM has no appointments yet; booking on clinic sites depends on it.
   Open questions: services and their durations, per-vet vs per-room availability, opening hours and
   exceptions, cancellation rules, whether online bookings need staff confirmation, payment up front
   (WebPay prototype exists in `sites/SPVetClinic/_wip`).
2. **F7 — Public API for clinic sites**: anonymous endpoints (services, availability, booking) with
   per-clinic CORS and rate limits, then `sites/SPVetClinic` consumes it.
3. **Product decision**: `Responsible` on exams/visits is free text; consider a staff picker.

Recorded decisions (UI view models, Contracts → Domain, migrations, one DB per clinic) are in
`docs/architecture/ARCHITECTURE.md`.

---

## Git History

See `git log --oneline` (kept out of this file so it never goes stale).

---

*This file is auto-updated manually after each significant change. Keep it in repo root for continuity across sessions.*