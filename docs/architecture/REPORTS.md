# Reports (management dashboard) — design

`GET api/reports/summary?from=&to=` (clinic-local dates, inclusive, at most 366 days), staff web → Reports.
Read-only: every figure comes from records the other modules already keep. Permission `reports.read`
(Admin, Manager).

| Area | Figures |
|---|---|
| Agenda | appointments, attended, upcoming, cancelled, no-shows and no-show % (of past appointments), online, overbooked; occupancy per vet/room; appointments per service |
| Billing | sales, voided, charged (sale totals), collected (payments by business date), average ticket, pending balance; by payment method; collected per day (per month in the chart for long ranges); top 10 sold |
| Inventory | items, units, stock value at cost and at sale price, low / out of stock (current, not per period) |
| Clinical | visits, doses applied; doses due soon / overdue (current) |

**Occupancy** = booked minutes of live appointments ÷ regular hours of the resource in the period
(weekly blocks with custom hours, closures and absences applied, × parallel capacity; overflow blocks
excluded). Overflow blocks and overbooking can push it above 100%.

Ceiling: computed on request from the transactional tables; fine for one clinic's volume. Add
pre-aggregated tables or a read replica only if a clinic's report becomes slow.
