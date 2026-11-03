# Inventory warehouses — design

## Model

A clinic keeps its stock in **warehouses** (`Warehouse`): for example a daily-use storeroom, a locked cabinet for
controlled drugs that only the head of the clinic opens, and a back storeroom with the supplies that restock the first.
The names are the clinic's data, typed in the staff web (Inventory → Warehouses).

- `Item.Stock` stays the clinic's **total**, so alerts, metrics, the reports dashboard and the alerts email read it as
  before.
- **The daily-use warehouse** (`IsDailyUse`, exactly one) holds whatever no other warehouse does: it has no stock rows,
  its units are the item's total minus the others'. The database is created with it (the migration names it in the
  clinic's language, `Localization:Culture`); it can be renamed, never archived or deleted.
- **Every other warehouse** keeps its units in `WarehouseStock` (warehouse, item, quantity).
- `InventoryMovement.WarehouseId` is the warehouse whose stock moved; null for the daily-use one (also every movement
  from before warehouses existed).

## What takes stock from where

Sales, visit supplies, vaccines applied from inventory, exam kits and patient consumption sheets always use the
**daily-use** warehouse, and their reversals (void, remove, delete, return) put the units back there. That is why none of
those records stores a warehouse: it can only be the daily-use one. A feature that lets the daily-use warehouse change, or
lets a visit take from another warehouse, needs a warehouse on those records first.

**Consumption sheets** (Inventory → Patient consumption) take what a patient uses as it's recorded: whole units for items
charged per unit, and one unit for each container started for items charged by container (an ampoule used little by
little on the same patient is taken once, when it's opened; throwing away its rest moves nothing). Items charged per mL
take nothing: the bottle is shared by several patients, so it's taken out by hand when one is opened. The sheet keeps
what it took per item (`ConsumptionStock`) and, after every change, brings it in line with what its uses need: removing a
use returns what no remaining use needs, deleting the sheet returns everything. A use is never refused for want of
stock, since losing the charge would be worse: what the daily-use warehouse can't cover stays pending on the sheet and is
taken on the sheet's next change, discharge and reopening included. The sheet keeps the units it still owes
(`StockPending`), so the sheets list shows it until they are taken, discharged or not; a discharged one is settled by a
supervisor reopening it and discharging it again once the warehouse has them. Sheets opened before this existed
(`MovesStock` false) move nothing, so their uses are still taken out by hand.

How an item is charged can't change (charge type, or container size) while a sheet not discharged yet uses it: the sheet
would add up the earlier uses again, changing both the charge and what it took from stock. Prices can.

A use that the daily-use warehouse can't cover is refused with the warehouses to ask, by name only ("Only 2 of 'X'
left in Daily use; ask Back storeroom for more"). The inventory screens move stock in a chosen warehouse (adjustment,
bulk ingress and egress) or between two (**transfer**: a `TransferOut` and a `TransferIn` movement, the total
unchanged). Metrics leave transfers out: they are neither use nor purchases.

Every change takes the clinic-wide `stock` lock ([exclusive work](../architecture/ARCHITECTURE.md#shared-mechanisms)),
warehouse changes included, so a warehouse can't be archived while a transfer fills it.

Every movement also leaves an entry in its item's audit (Inventory → Audit), saved with the movement: the inventory
screens write theirs, and `Stock.MoveAsync` writes the automatic ones (units, stock before and after, the daily-use
warehouse's units, and the reason, e.g. the exam report and patient). The record that moved the stock keeps its own
entry too. Automatic movements from before this existed have no audit entry; the item's movement history lists them.

## Who sees what

| Who | Sees | Moves stock by hand (with `inventory.update`) |
|---|---|---|
| Everyone with the inventory screens | The daily-use warehouse | In it |
| A user whose role a warehouse lists | That warehouse too | In it, and between the warehouses they see |
| `inventory.warehouses` (Admin only: no area grants it) | Every warehouse, archived ones in the admin page | In all of them |

A new warehouse is seen by no role until the admin marks some. What a user doesn't see stays out of everything the API
sends them: their stock is the units of the warehouses they see, the per-warehouse breakdown lists only those, the
movement history leaves the others' movements out, the stock filters and sort use the same number, and a realtime
stock notice names the item, never the new count (it reaches every connected user). When their units are 0 they read
"Ask *warehouse*" if another one holds some, "Sold out" if none does. Pickers (visits, sales, doses) and the exam
kit show the daily-use units, the ones those uses take.

Checking a warehouse's roles is the one place that reads role names, as clinic data; permissions still decide what a
user may do ([modules and roles](../architecture/ARCHITECTURE.md#shared-mechanisms)).

**Keeping a locked warehouse's count private.** Its purchases go straight into it (ingress or adjustment in that
warehouse, which the dialogs ask for when the user sees more than one), never through a warehouse others see: a
transfer from a shared warehouse shows the units leaving it. Hand-outs from the locked warehouse to the daily-use one
are visible, since they are the daily-use warehouse's own stock. Management screens (metrics, reports, audit) still show
totals to supervisors.

Open item lists refresh on their own when stock moves in the inventory screens, an exam takes or returns its kit, or a
consumption sheet takes or returns units. Sales, visit supplies and doses don't send that notice yet
(`Stock.NotifyChangedAsync`, one call after saving): the list catches up on its next load.

## Archiving and deleting

Only an empty warehouse is archived (hidden from the stock screens, restorable). Only a warehouse that never had a
movement is deleted; the daily-use one is neither.

## API

| Endpoint | Permission | |
|---|---|---|
| `GET api/inventory/warehouses` | `inventory.read` | The warehouses the user sees (all, with roles and archived ones, for `inventory.warehouses`) |
| `GET api/inventory/warehouses/roles` | `inventory.warehouses` | Roles a warehouse can list |
| `POST`, `PUT {id}`, `DELETE {id}` `api/inventory/warehouses` | `inventory.warehouses` | Create, rename / set roles / archive, delete |
| `POST api/inventory/movements/transfer` | `inventory.update` | One item between two warehouses; refused if the first lacks the units |
| `POST api/inventory/movements/mass-transfer` | `inventory.update` | Several items; each moves at most what the first warehouse holds |
| `POST .../adjust-stock` (`WarehouseId`), `.../mass-ingress` and `.../mass-egress` (`?warehouseId=`) | `inventory.update` | In that warehouse; the daily-use one when missing |

Items (`api/items`, `paged`, by id and barcode) carry `Stock` (what the user sees), `DailyUseStock`, `Warehouses`
(units per warehouse they see) and `AskAt` (names of the other warehouses holding some).

## Later

Per-warehouse minimums with a "to restock" list (what to move from the back storeroom to the daily-use one): low-stock
alerts read the total until then.
