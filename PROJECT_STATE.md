# VetManagement — Project State

**Last updated:** 2026-10-03 · **Branch:** main · **Tests:** 253 (+3 on SQL Server) · CI green

Product model: one generic staff CRM sold to many clinics; each clinic has its own deployment and database, its
own branded public site, and configures every business policy itself (admin-editable settings).
How the code is organised: [`docs/architecture/ARCHITECTURE.md`](docs/architecture/ARCHITECTURE.md). All docs:
[`docs/README.md`](docs/README.md).

## Phases

| Phase | Status | What |
|---|---|---|
| F0–F5 | ✅ | Restructure into Clean Architecture, CI, inventory/clients/exams/medical/audit moved to Domain + Contracts, staff UI split out, hardening (errors, login protection, health check, logs), architecture docs |
| F6 | ✅ | Agenda: per-clinic settings, availability with progressive release, appointments, staff agenda, emails — [SCHEDULING.md](docs/architecture/SCHEDULING.md) |
| F7 | ✅ | Online booking: RUT, WebPay Plus / simulated payments, public API, hosted portal, link from SPVetClinic — [SCHEDULING.md](docs/architecture/SCHEDULING.md), [DEPLOYMENT.md](docs/DEPLOYMENT.md) |
| F8 | ✅ | Billing: sales, split payments, discounts, voids, daily cash close, stock on sale — [BILLING.md](docs/architecture/BILLING.md) |
| F9 | ✅ | Clinical record: visit notes, vaccines/deworming with protocols, due list, owner reminders — [CLINICAL.md](docs/architecture/CLINICAL.md) |
| F10 | ✅ | Inventory from clinical use: visit supplies and vaccines leave stock; shared stock lock — [CLINICAL.md](docs/architecture/CLINICAL.md) |
| F11 | ✅ | Reports dashboard — [REPORTS.md](docs/architecture/REPORTS.md) |
| F12 | ✅ | Client portal "Mis mascotas" — [CLIENT_PORTAL.md](docs/architecture/CLIENT_PORTAL.md) |
| — | ✅ | "Charge visit": appointment + procedures + supplies, one sale or split taxed / VAT-exempt; legacy visit payment fields retired — [BILLING.md](docs/architecture/BILLING.md) |
| — | ✅ | Test data page for development (Administration → Test Data) — [guide](docs/guides/DATOS_DE_PRUEBA.md) |

## Blocked on the clinic (not code)

1. Transbank production credentials (commerce code + API key secret) for each clinic.
2. SMTP account for client emails (confirmations, reminders, client-portal links).
3. Hosting: API + SQL Server database, staff web, booking portal — follow [DEPLOYMENT.md](docs/DEPLOYMENT.md).
4. Which items are VAT-exempt (clinic's accountant), then set it in Billing settings → `tax`.
5. Switch `sites/SPVetClinic` `BookingUrl` from the external CRM to the portal once live.

## Open product decisions

- Should charging an appointment mark it `Completed`? Refunds of paid sales and reopening a closed day are not built.
- `Responsible` on exams/visits is free text; a staff picker would link records to users.
- Printable prescriptions; attachments (images, lab PDFs) in the clinical record.

## Later (depend on providers or costs)

WhatsApp notifications (setting reserved) · electronic receipts (boleta/factura SII) · services needing a vet *and*
a room at once · offline mode for the desktop app.

## Quick checks

```bash
dotnet build VetManagement.sln
dotnet test tests/VetManagement.Tests/VetManagement.Tests.csproj
git log --oneline -15
```

History of what changed and why lives in `git log`; this file only says where things stand.
