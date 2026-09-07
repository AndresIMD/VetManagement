# Architectural migration boundaries

## Target dependency direction

```text
Api / AdminWeb / ClientPortal / Mobile
             ↓
        Application ← Contracts
             ↓
       Domain ← Infrastructure
```

`Domain` has no project or package dependencies. `Contracts` contains only API transport types.
`Shared` is temporarily retained for existing Razor UI and legacy models; no new domain entity,
API contract, application service, or persistence abstraction may be added there.

## Transitional rules

1. New use cases must be organised under `Application/Features/<Module>/<UseCase>`.
2. Controllers must receive request contracts and return response contracts, never EF entities.
3. New inventory changes must create an immutable movement; they may not set `Item.Stock`.
4. Each migrated feature removes, rather than duplicates, its legacy service path.
5. New tests belong to the relevant layer-specific test project. The legacy test project remains only until its tests are moved.
