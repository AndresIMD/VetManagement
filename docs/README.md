# Documentation

| Document | What it covers |
|---|---|
| [`../PROJECT_STATE.md`](../PROJECT_STATE.md) | What is built, what is blocked on the clinic, open product decisions, roadmap |
| [`architecture/ARCHITECTURE.md`](architecture/ARCHITECTURE.md) | Projects and dependency rules, module pattern, guard tests, operations, recorded decisions |
| [`CODING_STANDARDS.md`](CODING_STANDARDS.md) | Code style and patterns for anyone (person or AI) changing the code |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | Deploying for a clinic: every configuration key, which ones are secret |

## Module designs

One document per business module: rules, per-clinic settings, permissions and known limits.

| Module | Document |
|---|---|
| Agenda and online booking | [`architecture/SCHEDULING.md`](architecture/SCHEDULING.md) |
| Billing (caja), VAT, "Charge visit", cash close | [`architecture/BILLING.md`](architecture/BILLING.md) |
| Clinical record, vaccines, supplies used in visits | [`architecture/CLINICAL.md`](architecture/CLINICAL.md) |
| Reports dashboard | [`architecture/REPORTS.md`](architecture/REPORTS.md) |
| Client portal "Mis mascotas" | [`architecture/CLIENT_PORTAL.md`](architecture/CLIENT_PORTAL.md) |

Inventory, clients/pets, exams and users follow the general module pattern in `ARCHITECTURE.md`; they have no
separate document.

## Guides (Spanish, for clinic staff and testers)

| Guide | |
|---|---|
| [`guides/DATOS_DE_PRUEBA.md`](guides/DATOS_DE_PRUEBA.md) | Start the system and fill a development database with sample data |

## Keeping the docs current

- A change to a module's rules or settings updates its design document in the same commit.
- `PROJECT_STATE.md` changes when a phase finishes or a decision is made; history lives in `git log`, not in docs.
- Obsolete documents are deleted, not archived: git keeps them (`git log --diff-filter=D --name-only -- docs`).
