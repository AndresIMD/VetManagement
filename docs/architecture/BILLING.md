# Billing (caja) — design

Principle: the clinic's billing **policy** is per-clinic configuration (admin-editable); the money rules
(totals, balances, closed days) are fixed in code so every clinic's books stay consistent.

## Settings (key `billing`, versioned like the agenda settings)

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Module on/off |
| `PaymentMethods` | Efectivo (cash), Débito, Crédito, Transferencia | Stable `Code`, display `Name`, `Enabled`, `IsCash` (counted in the drawer) |
| `MaxDiscountPercent` | `10` | Largest discount per line without `billing.manage` |
| `DeductStockOnSale` | `true` | Paying a sale takes its products out of inventory; a void puts them back |

Staff web → Billing → Settings. Never change a method's `Code` once used (payments store it).
`OnlineDeposit` is reserved for deposits paid when booking online.

## Sale

`Open` → `Paid` (balance reaches 0) or `Voided` (with a reason, `billing.manage` only).

- Lines: `Service` (agenda catalog name/price), `Product` (inventory item; cannot exceed stock when
  `DeductStockOnSale`), `Other` (free text + price). Empty price = catalog price. Discount in pesos per line.
- Payments: any enabled method, never above the balance; split payments allowed.
- From an appointment (Agenda → Charge): one live sale per appointment; it starts with the service at the
  booked price and the online deposit as a payment (`OnlineDeposit`), so only the balance is charged.
- All amounts are whole CLP; totals are computed, never stored.

## Daily cash close

Payments carry the clinic-local business date. The cash close shows totals per method and the expected
cash (methods with `IsCash`); staff enter the counted cash and a note. After closing, that day takes no
payments and its sales can't be voided. One close per day.

## Permissions

| Permission | Admin | Manager | Employee |
|---|---|---|---|
| `billing.read` — sales, cash summary | ✓ | ✓ | ✓ |
| `billing.charge` — open sales, lines, payments, close the day | ✓ | ✓ | ✓ |
| `billing.manage` — void, discounts above the limit, settings | ✓ | | |

## Concurrency

Every billing write runs under one clinic-wide `sp_getapplock` (`billing`), so two desks can't overpay a
sale and no payment slips past a day being closed (`ConcurrentPaymentTests`, real SQL Server).

## Not yet

Electronic receipts (boleta/factura SII), refunds of paid sales, reopening a closed day, client account
balances. The appointment's status is not changed by charging it.
