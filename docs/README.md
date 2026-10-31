# Documentation

| Document | What it covers |
|---|---|
| [`../PROJECT_STATE.md`](../PROJECT_STATE.md) | What is built, what is blocked on the clinic, open product decisions, roadmap |
| [`../DESIGN.md`](../DESIGN.md) | How the product should feel: positioning, design rules, visual direction |
| [`architecture/ARCHITECTURE.md`](architecture/ARCHITECTURE.md) | Projects and dependency rules, module pattern, guard tests, operations, recorded decisions |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | Code style and patterns for anyone (person or AI) changing the code |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | Deploying for a clinic: every configuration key, which ones are secret |

## Module designs

One document per business module: rules, per-clinic settings, permissions and known limits.

| Module | Document |
|---|---|
| Agenda and online booking | [`modules/SCHEDULING.md`](modules/SCHEDULING.md) |
| Billing (caja), VAT, "Charge visit", cash close | [`modules/BILLING.md`](modules/BILLING.md) |
| Clinical record, vaccines, supplies used in visits | [`modules/CLINICAL.md`](modules/CLINICAL.md) |
| Inventory warehouses: daily-use stock, transfers, who sees each warehouse | [`modules/INVENTORY.md`](modules/INVENTORY.md) |
| Reports dashboard | [`modules/REPORTS.md`](modules/REPORTS.md) |
| Exam reports (printable lab results, templates, branding) | [`modules/EXAM_REPORTS.md`](modules/EXAM_REPORTS.md) |
| Client portal "Mis mascotas" | [`modules/CLIENT_PORTAL.md`](modules/CLIENT_PORTAL.md) |

Inventory items, clients/pets, exams and users follow the general module pattern in `ARCHITECTURE.md`; they have no
separate document.

## Guides (for clinic staff and testers)

| Guide | |
|---|---|
| [`guides/TEST_DATA.md`](guides/TEST_DATA.md) | Start the system and fill a development database with sample data |

## Keeping the docs current

- A change to a module's rules or settings updates its design document in the same commit.
- `PROJECT_STATE.md` changes when a phase finishes or a decision is made; history lives in `git log`, not in docs.
- Docs are written in English; only client-facing text in the product is Spanish.
- Obsolete documents are deleted, not archived: git keeps them (`git log --diff-filter=D --name-only -- docs`).
