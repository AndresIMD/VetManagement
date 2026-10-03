# Architecture

VetManagement is a **modular monolith** organised with Clean Architecture. One codebase serves every
clinic: the staff CRM and the booking portal are the same product for all of them, while each clinic has
its own deployment (database, API) and its own branded public website.

## Projects and dependency direction

```text
   Staff.Web (WASM)   Staff.Maui (desktop)       Booking.Web (WASM)        sites/<Clinic> (public web)
            \             /                    booking + client portal      links to Booking.Web
             Staff.UI (pages, components,               |                            |
             view models, API clients)                  |                            |
                        |                               |                            |
                        +--------------- HTTP (JSON) ---+----------------------------+
                                                |
  Api (controllers, auth, migrations, jobs) ──> Application (use cases) ──> Domain (entities, enums)
        |                                              ^
        └──> Infrastructure (EF Core, gateways) ───────┘
  Contracts (requests/DTOs) is shared by Api and every client; it depends only on Domain (enums).
```

| Project | Responsibility | May reference |
|---|---|---|
| `Domain` | Entities, enums, domain rules (sale totals, RUT, availability, settings validation). No attributes, no persistence. | nothing |
| `Contracts` | HTTP requests/DTOs with server-side validation. The wire format between API and clients. | Domain |
| `Application` | Services/use cases, repository interfaces, per-clinic settings, stock changes. | Domain, Contracts |
| `Infrastructure` | `AppDbContext`, EF configuration (column lengths, indexes), repositories, payment gateways. | Domain, Application |
| `Api` | Controllers (map request → entity → DTO), auth policies, EF migrations, background jobs, test data (dev only). | Application, Infrastructure, Contracts |
| `Staff.UI` | Staff UI shared by web and MAUI: pages, components, view models, `*ApiService` clients. | Domain, Contracts |
| `Staff.Web` / `Staff.Maui` | Thin hosts for `Staff.UI`. | Staff.UI |
| `Booking.Web` | Public portal: online booking with deposit, booking status/cancel, client portal ("Mis mascotas"). Anonymous. | Contracts |
| `sites/*` | One branded public website per clinic. Not part of the product. | (links to Booking.Web) |

The backend never references `Staff.UI`. `ArchitectureTests` fails the build if that changes.

## Module pattern

Modules: Inventory, Clients/Pets, Exams, Medical/Clinical, Audit, Accounts, Scheduling, Billing, Reports,
Client portal. Each follows the same shape:

| Layer | Example (Billing) |
|---|---|
| Domain entity + rules, no attributes | `Domain/Billing/Sale.cs` |
| Request + DTO with validation | `Contracts/Billing/BillingDtos.cs` |
| Service | `Application/Billing/BillingService.cs` |
| EF configuration + repository | `Infrastructure/Data/AppDbContext.cs`, `Infrastructure/Repositories/SaleRepository.cs` |
| Controller + mapping | `Api/Controllers/Billing/BillingController.cs` |
| UI view model / DTO + API client + pages | `Staff.UI/Services/Api/BillingApiService.cs`, `Staff.UI/Pages/Billing/*` |

Rules:

1. Controllers receive request contracts and return DTOs, never EF entities.
2. The id of the resource comes from the route only; request bodies never carry ids of the root entity
   (child items keep theirs so updates modify existing rows). Related entities are linked by id, never
   created or overwritten through another resource's body.
3. Server validation lives on the request DTO and must cover at least what the UI enforces.
4. The older modules keep UI view models (form messages, `Drug : Item` polymorphism) with the same JSON
   shape as the DTOs; newer modules bind pages to the Contracts DTOs directly. `WireContractTests` guards
   the view models.
5. Stock changes only with a movement: `Application/Inventory/Stock.MoveAsync` (sales, visit supplies,
   doses) or the inventory screens. Never set `Item.Stock` without one.
6. Business policies are per-clinic settings, never constants (see below).

## Shared mechanisms

**Per-clinic settings.** Each module with policies has one versioned JSON document in `ClinicSettings`
(`scheduling`, `billing`, `clinical`), served by a `ClinicSettingsService<T>`: created from defaults on
first read, validated on save (every error listed), optimistic concurrency (409 on a stale version),
audited. Admins edit them as JSON in the staff web (Agenda / Billing / Clinical settings).

**Exclusive work.** `IUnitOfWork.ExecuteExclusiveAsync(key, work)` runs work in a transaction holding a
SQL Server application lock (`sp_getapplock`), retried as a whole on transient errors. Keys: per resource
for bookings (two people can't take the last slot) and `stock` for billing and every stock change.
Serializable isolation alone deadlocked under contention; the concurrency tests run on real SQL Server.

**Background job.** `AppointmentRemindersHostedService` (every 5 min): appointment reminders, preventive
dose reminders, expiry of unpaid online holds. Emails never fail the operation that triggered them.

**Public endpoints.** `api/public/*` are anonymous and rate limited per IP (`public-read` / `public-write`).
Client secrets (booking tokens, portal links) are random; portal links are stored hashed.

## Guard tests

| Test | Fails when |
|---|---|
| `ArchitectureTests` | the backend references `Staff.UI`, or `Domain` references another project |
| `MigrationsTests` | the EF model differs from the latest migration snapshot (lists every pending operation) |
| `WireContractTests` | UI view models and API DTOs stop round-tripping the same JSON |
| `AuthorizationPoliciesTests` / `UiPolicyParityTests` | a policy used by a controller or page isn't registered, or UI and API policies drift |
| `UiRoutesTests` | the staff UI calls an API route that doesn't exist |
| `ErrorHandlingTests`, `HealthCheckTests`, `LoginProtectionTests`, `LoginPermissionSyncTests` | error handling, health, brute-force protection or permission sync regress |
| `RequestValidationTests` | orders/visits can be stored without a patient |
| Scheduling: `AvailabilityCalculatorTests`, `SchedulingSettings*Tests`, `AppointmentsApiTests`, `AppointmentNotificationsTests`, `OnlineBookingTests`, `PaymentGatewayTests`, `RutTests` | agenda, booking, payment or RUT rules regress |
| Billing: `SaleTests`, `BillingApiTests`, `CashCloseTests`, `VisitChargeTests` | sale, payment, VAT, cash close or visit-charge rules regress |
| Clinical: `ClinicalApiTests`, `ClinicalStockTests`, `ClientPortalTests` | clinical record, stock from clinical use or portal access rules regress |
| `ReportsApiTests`, `TestDataTests` | report figures or sample-data generators break |
| SQL Server (opt-in, always in CI): `ConcurrentBookingTests`, `ConcurrentPaymentTests`, `TestDataSqlServerTests` | locks fail under parallel requests, or data doesn't fit the real schema |

## Operations

- Errors: `ProblemDetails` everywhere; unhandled exceptions are logged and return a 500 without internals.
- Logs: JSON console logs with scopes (`TraceId`) in Production.
- Health: `/healthz` checks database connectivity (503 when unreachable).
- Login: account lockout (5 failures → 5 min) plus 10 attempts/min per IP. Permissions are claims in the
  token, synced at login: after an update that adds permissions, users log in again.
- Secrets: user-secrets in Development only; environment variables in deployments; never in `appsettings*.json`.
  Every key is listed in [`../DEPLOYMENT.md`](../DEPLOYMENT.md).

## Decisions

- **UI view models are kept** in the older modules instead of binding pages to DTOs: they carry form
  validation messages and polymorphism; mapping lives only in the `*ApiService` classes.
- **Contracts depends on Domain** for enums. Domain has no dependencies, so clients pay almost nothing;
  revisit only if Contracts is published as a NuGet package.
- **Applied migrations are never edited.** A few early migration designer files still name entities by
  their pre-refactor CLR types (`VetManagement.Shared.Models.*`); harmless, the current snapshot is clean.
- **`Responsible`** on exam orders and visits is free text (the clinically responsible person, who may
  differ from whoever records it). The audit log always stores the authenticated user.
- **One database per clinic** is the multi-clinic model (data isolation for medical records).
- **Billing is the only source of truth for charges.** Visits keep legacy payment fields in the data but
  no screen edits them.
- **VAT rules are configuration, not code**: which items are exempt depends on each clinic's tax situation.
