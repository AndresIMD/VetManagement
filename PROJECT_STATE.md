# VetManagement — Project State

**Last updated:** 2026-10-23 · **Branch:** main (work on feature branches, merged by pull request) · **Tests:** 423 (with SQL Server)

Product model: one generic staff CRM sold to many clinics; each clinic has its own deployment and database, its
own branded public site, and configures every business policy itself (admin-editable settings).
How the code is organised: [`docs/architecture/ARCHITECTURE.md`](docs/architecture/ARCHITECTURE.md). All docs:
[`docs/README.md`](docs/README.md).

## Phases

| Phase | Status | What |
|---|---|---|
| F0–F5 | ✅ | Restructure into Clean Architecture, CI, inventory/clients/exams/medical/audit moved to Domain + Contracts, staff UI split out, hardening (errors, login protection, health check, logs), architecture docs |
| F6 | ✅ | Agenda: per-clinic settings, availability with progressive release, appointments, staff agenda, emails — [SCHEDULING.md](docs/modules/SCHEDULING.md) |
| F7 | ✅ | Online booking: RUT, WebPay Plus / simulated payments, public API, hosted portal, link from a clinic's public site — [SCHEDULING.md](docs/modules/SCHEDULING.md), [DEPLOYMENT.md](docs/DEPLOYMENT.md) |
| F8 | ✅ | Billing: sales, split payments, discounts, voids, daily cash close, stock on sale — [BILLING.md](docs/modules/BILLING.md) |
| F9 | ✅ | Clinical record: visit notes, vaccines/deworming with protocols, due list, owner reminders — [CLINICAL.md](docs/modules/CLINICAL.md) |
| F10 | ✅ | Inventory from clinical use: visit supplies and vaccines leave stock; shared stock lock — [CLINICAL.md](docs/modules/CLINICAL.md) |
| F11 | ✅ | Reports dashboard — [REPORTS.md](docs/modules/REPORTS.md) |
| F12 | ✅ | Client portal "Mis mascotas" — [CLIENT_PORTAL.md](docs/modules/CLIENT_PORTAL.md) |
| — | ✅ | "Charge visit": appointment + procedures + supplies, one sale or split taxed / VAT-exempt; legacy visit payment fields retired — [BILLING.md](docs/modules/BILLING.md) |
| — | ✅ | Modules per clinic (`Modules:Enabled`) and area roles (Inventory, Laboratory, Reception, Clinical, Cashier + Supervisor) — [ARCHITECTURE.md](docs/architecture/ARCHITECTURE.md) |
| — | ✅ | Exam reports: per-exam templates with normal ranges (↓/↑), interpretation bands, multi-value interpretation rules, reference column and IDEXX-style bars, previous result column, highlighted qualitative results, reports for unregistered (typed) patients; clinic branding (logo, background, font, footer, centered or left-aligned columns); multi-page print / save PDF; the exam's test kit deducted from inventory from the report — [EXAM_REPORTS.md](docs/modules/EXAM_REPORTS.md) |
| — | ✅ | Inventory warehouses: a daily-use one for sales, visits, doses and exam kits, others seen only by their roles (where to ask, never their count), transfers — [INVENTORY.md](docs/modules/INVENTORY.md) |
| — | ✅ | Moving a clinic's data from a legacy app (the copy script lives with that clinic's deployment) — [DEPLOYMENT.md](docs/DEPLOYMENT.md#moving-a-clinic-from-another-app) |
| — | ✅ | Test data page for development (Settings → Test Data) — [guide](docs/guides/TEST_DATA.md) |
| — | ✅ | Language per clinic: staff UI, printed reports, server messages, client emails and default settings in the clinic's language (English and Spanish, Chilean terms on top); the booking portal is still Spanish only — [DEPLOYMENT.md](docs/DEPLOYMENT.md) |

## Blocked on the clinic (not code)

1. Transbank production credentials (commerce code + API key secret) for each clinic.
2. SMTP account for client emails (confirmations, reminders, client-portal links).
3. Hosting: API + SQL Server database, staff web, booking portal — follow [DEPLOYMENT.md](docs/DEPLOYMENT.md).
4. Which items are VAT-exempt (clinic's accountant), then set it in Billing settings → `Tax`.
5. Point the booking buttons of the clinic's public site (`BookingUrl`) to the portal once live.

## Open product decisions

- Should charging an appointment mark it `Completed`? Refunds of paid sales and reopening a closed day are not built.
- `Responsible` on exams/visits is free text; a staff picker would link records to users.
- Exam reports: emailing the PDF from the server and starting a report from an exam request item are not built.
- Report fonts: beyond the system fonts and the bundled open-licence ones (Inter, Figtree, Jost), either a button to
  upload the clinic's own font (stored like the logo, embedded in the report; the clinic must hold a web/app embedding
  licence, e.g. for Futura or Aptos) or more bundled open-licence fonts.
- Printable prescriptions; attachments (images, lab PDFs) in the clinical record.
- Settings screens (agenda, billing, clinical) are JSON editors that point admins to repo docs they can't open;
  guided forms with in-screen explanations would suit clinic admins.
- How long to keep personal data: the audit log keeps every change for good, client changes with the whole record
  (name, tax ID, contact details), also after the client is deleted. Chile's Ley 21.719 (in force from December 2026)
  asks to keep personal data only while it is needed and to delete it when the person asks. Decide a retention period
  and whether old entries keep client details, with the clinic's lawyer; a background job can then purge or redact.

## Later (depend on providers or costs)

- Enforcing paid modules once there is a second clinic: `Modules:Enabled` is configuration the clinic could edit on
  its own server. Either host the CRM (vendor-run, subdomain per clinic) or read the modules from a license signed by
  the vendor; `EnabledModules` is the single place to change.
- WhatsApp notifications (setting reserved).
- Electronic receipts (boleta/factura SII).
- Services needing a vet *and* a room at once.
- Offline mode for the desktop app.

## Quick checks

```bash
dotnet build VetManagement.sln
dotnet test tests/VetManagement.Tests/VetManagement.Tests.csproj
git log --oneline -15
```

History of what changed and why lives in `git log`; this file only says where things stand.
