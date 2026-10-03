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
| **F6** | ✅ **COMPLETED** | Scheduling: per-clinic settings, availability with progressive release, appointments, staff agenda, emails (docs/architecture/SCHEDULING.md) |
| **F7** | ✅ **COMPLETED** | Online booking: RUT, payment gateways (WebPay Plus / simulated), public API, hosted portal `Booking.Web`, SPVetClinic link (docs/DEPLOYMENT.md) |
| **F8** | ✅ **COMPLETED** | Billing (caja): sales from appointments or counter, split payments, discounts, voids, daily cash close, stock deducted on sale (docs/architecture/BILLING.md) |
| **F9** | ✅ **COMPLETED** | Clinical record: visit notes linked to appointments, vaccines/deworming with per-clinic protocols, due list, owner reminders (docs/architecture/CLINICAL.md) |
| **F10** | ✅ **COMPLETED** | Inventory from clinical use: supplies used in visits and vaccines applied leave stock, back on removal; shared stock lock |
| **F11** | ✅ **COMPLETED** | Reports dashboard: agenda occupancy and no-shows, income by method/day, top sold, stock value, clinical activity (docs/architecture/REPORTS.md) |
| **F12** | ✅ **COMPLETED** | Client portal "Mis mascotas": emailed access link by RUT, vaccines, visits, upcoming appointments (docs/architecture/CLIENT_PORTAL.md) |

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

## What is done (verified: 246 tests + 2 SQL Server concurrency tests, CI green, browser end-to-end)

- Staff CRM modules: inventory (items, movements, alerts), clients and pets, exams (orders, performed, external labs),
  medical visits, audit log, users/roles/permissions, email.
- Agenda (F6): per-clinic settings (DB + JSON defaults, versioned, admin-editable), vets/rooms, holidays and
  absences, overbooking, progressive release / sobrecupo, staff booking/reschedule/cancel, emails and reminders.
- Online booking (F7): hosted portal `Booking.Web` with clinic branding, RUT validation, auto-created clients/pets,
  deposit via WebPay Plus (sandbox-verified) or simulated gateway, refund modes, cancellation link.
- Billing (F8): sales (from an appointment with its online deposit, or at the counter), service/product/other lines,
  split payments with per-clinic payment methods, discount limit, voids, daily cash close, stock deducted on sale.
- Clinical record (F9): visit notes (reason, anamnesis, exam, diagnosis, treatment, weight, temperature) linked to
  appointments; vaccines and deworming with per-clinic protocols and next due dates; due list; owner email reminders.
- Inventory from clinical use (F10): drugs/materials used in a visit and vaccines from inventory leave stock with a
  traceable movement; removing them (or deleting the visit/dose) puts stock back. Configurable per clinic.
- Reports (F11): management dashboard for any period (Admin, Manager).
- Client portal (F12): owners see their pets' vaccines, visits and upcoming appointments via an emailed link (no account).
- Hardening: ProblemDetails, login lockout + rate limits, health check `/healthz`, JSON logs, guard tests.
- Docs: `docs/architecture/ARCHITECTURE.md`, `docs/architecture/SCHEDULING.md`, `docs/architecture/BILLING.md`, `docs/architecture/CLINICAL.md`, `docs/architecture/REPORTS.md`, `docs/architecture/CLIENT_PORTAL.md`, `docs/DEPLOYMENT.md`.

## Blocked on the clinic (not code)

1. Transbank production credentials (commerce code + API key secret) for each clinic.
2. SMTP account for client emails.
3. Hosting: API + SQL Server database, staff web, booking portal; follow `docs/DEPLOYMENT.md`.
4. Switch `sites/SPVetClinic` `BookingUrl` from the external CRM to the portal once live.

## Open product decisions

- `Responsible` on exams/visits is free text; a staff picker would link records to users.
- Billing: should charging an appointment mark it `Completed`? Refunds of paid sales and reopening a closed day are not built.
- Medical visits still carry legacy payment status/method fields that overlap billing (F8); hide them?
- Charge a visit's supplies in billing ("Charge visit" building the sale from the record)?
- Prescriptions as printable documents; attachments (images, lab PDFs) in the clinical record.

## Roadmap (proposed, not started)

| Phase | Feature | Why |
|---|---|---|
| Later | WhatsApp notifications (setting reserved); services needing a vet *and* a room; electronic receipts (SII) | Depend on providers/costs |

Recorded decisions (UI view models, Contracts → Domain, migrations, one DB per clinic) are in
`docs/architecture/ARCHITECTURE.md`.

---

## Git History

See `git log --oneline` (kept out of this file so it never goes stale).

---

*This file is auto-updated manually after each significant change. Keep it in repo root for continuity across sessions.*