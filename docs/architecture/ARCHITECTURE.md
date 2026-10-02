# Architecture

VetManagement is a **modular monolith** organised with Clean Architecture. One codebase serves every
clinic: the staff CRM is the same product for all of them, while each clinic has its own branded
public website.

## Projects and dependency direction

```text
                 Staff.Web (WASM)   Staff.Maui (desktop/mobile)      sites/<Clinic> (public web)
                          \             /                                   |
                           Staff.UI (pages, components, view models, API clients)
                                        |                                   |
                                        +--------- HTTP (JSON) -------------+
                                                         |
  Api (controllers, auth, migrations) ──> Application (use cases) ──> Domain (entities, enums)
        |                                      ^
        └──> Infrastructure (EF Core) ─────────┘
  Contracts (requests/DTOs) is shared by Api and every client; it depends only on Domain (enums).
```

| Project | Responsibility | May reference |
|---|---|---|
| `Domain` | Entities, enums, domain rules (`InventoryReasons`, enum mappings). No attributes, no persistence. | nothing |
| `Contracts` | HTTP requests/DTOs with server-side validation. The wire format between API and clients. | Domain |
| `Application` | Services/use cases, repository interfaces, `PagedResult`. | Domain, Contracts |
| `Infrastructure` | `AppDbContext`, EF configuration (column lengths, owned types), repositories. | Domain, Application |
| `Api` | Controllers (map request → entity → DTO), auth policies, EF migrations, hosting. | Application, Infrastructure, Contracts |
| `Staff.UI` | Staff UI shared by web and MAUI: pages, components, view models, `*ApiService` clients. | Domain, Contracts |
| `Staff.Web` / `Staff.Maui` | Thin hosts for `Staff.UI`. | Staff.UI |
| `sites/*` | One branded public website per clinic. Not part of the product. | (HTTP to Api) |

The backend never references `Staff.UI`. `ArchitectureTests` fails the build if that changes.

## Module pattern

Every module (Inventory, Clients/Pets, Exams, Medical, Audit, Accounts) follows the same shape:

| Layer | Example (Clients) |
|---|---|
| Domain entity, no attributes | `Domain/Clients/Client.cs` |
| Request + DTO with validation | `Contracts/Clients/ClientDtos.cs` (`ClientRequest`, `ClientDto`) |
| Service | `Application/Services/ClientPetService.cs` |
| EF configuration + repository | `Infrastructure/Data/AppDbContext.cs`, `Infrastructure/Repositories/ClientRepository.cs` |
| Controller + mapping | `Api/Controllers/Clients/ClientPetController.cs` |
| UI view model + API client | `Staff.UI/Models/Core/Client.cs`, `Staff.UI/Services/Api/ClientPetApiService.cs` |

Rules:

1. Controllers receive request contracts and return DTOs, never EF entities.
2. The id of the resource comes from the route only; request bodies never carry ids of the root entity
   (child items keep theirs so updates modify existing rows). Related entities are linked by id, never
   created or overwritten through another resource's body.
3. Server validation lives on the request DTO and must cover at least what the UI enforces.
4. The UI keeps its own view models (form messages, `Drug : Item` polymorphism) with the same JSON shape
   as the DTOs; `WireContractTests` guards that shape.
5. Inventory stock changes only through immutable movements; never set `Item.Stock` directly.

## Guard tests

| Test | Fails when |
|---|---|
| `ArchitectureTests` | the backend references `Staff.UI`, or `Domain` references another project |
| `MigrationsTests` | the EF model differs from the latest migration snapshot (lists every pending operation) |
| `WireContractTests` | UI view models and API DTOs stop round-tripping the same JSON |
| `AuthorizationPoliciesTests` | a controller uses an `[Authorize(Policy)]` that is not registered |
| `ErrorHandlingTests` | an unhandled exception leaks details or isn't returned as ProblemDetails |
| `LoginLockoutTests` / `LoginRateLimitTests` | brute-force protection on login regresses |
| `RequestValidationTests` | orders/visits can be stored without a patient |

## Operations

- Errors: `ProblemDetails` everywhere; unhandled exceptions are logged and return a 500 without internals.
- Logs: JSON console logs with scopes (`TraceId`) in Production.
- Health: `/healthz` checks database connectivity (503 when unreachable).
- Login: account lockout (5 failures → 5 min) plus 10 attempts/min per IP.
- Secrets: user-secrets in Development only; never in `appsettings*.json`.

## Decisions

- **UI view models are kept** instead of binding pages to DTOs: they carry form validation messages and
  polymorphism; mapping lives only in the `*ApiService` classes.
- **Contracts depends on Domain** for enums. Domain has no dependencies, so clients pay almost nothing;
  revisit only if Contracts is published as a NuGet package.
- **Applied migrations are never edited.** Older snapshots still name entities by their pre-refactor CLR
  types (`VetManagement.Shared.Models.*`); this is harmless and refreshes with the next migration.
- **`Responsible`** on exam orders and visits is free text (the clinically responsible person, who may
  differ from whoever records it). The audit log always stores the authenticated user.
- **One database per clinic** is the intended multi-clinic model (data isolation for medical records).
