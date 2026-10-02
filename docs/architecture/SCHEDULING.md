# Scheduling (agenda) — design

Principle: **every business policy is per-clinic configuration**, never a constant in code. A clinic's
admin edits it in the staff web before activating the agenda (`Enabled = false` by default).

## Configuration

One versioned settings document per clinic, stored in the database (`ClinicSettings`, key
`scheduling`). On first use it is created from `scheduling.defaults.json` (placeholder values), then
the admin edits it. Every change is validated, versioned (optimistic concurrency) and audited.

| Section | Settings |
|---|---|
| General | `Enabled`, `TimeZone` (IANA, default `America/Santiago`) |
| Booking | minimum notice, booking horizon (days), slot step |
| Cancellation | client deadline (hours), refund mode: `Automatic` / `NoRefund` / `ManualApproval` |
| Payment | pending-payment hold (minutes) while WebPay confirms |
| Notifications | each one toggled separately: confirmation, reminder (+ hours before), reschedule notice; WhatsApp reserved for later |
| Services | stable `Code`, name, `Enabled` (module on/off), duration, buffer, price, deposit %, online booking allowed, resources that can perform it |
| Resources | stable `Code`, name, kind (`Vet` / `Room`), enabled, weekly blocks, progressive release |
| Exceptions | date range, whole clinic or one resource, kind `Closed` / `Absence` / `CustomHours` (+ blocks), reason |

Services are enabled independently, so a clinic can offer any combination (e.g. consultations and
vaccines only).

## Availability

`AvailabilityCalculator` (Domain, pure, fully unit-tested) returns bookable slots for a service and
date range from the settings, the existing appointments and the current time.

- A day's hours come from the resource's weekly blocks, replaced by `CustomHours` exceptions and
  removed by `Closed` / `Absence` exceptions.
- A block has a capacity (`MaxParallel`) so a resource can take overlapping bookings when allowed.
- **Progressive release** (public audience only): blocks are ordered; the next block opens only when
  the previous ones reach the release threshold (% of booked minutes). A block can be flagged
  `IsOverflow` so the public sees it as extra capacity ("sobrecupo"). This keeps specialists' bookings
  close together instead of two patients at 10:00 and 17:00.
- Public audience also applies minimum notice and booking horizon; staff sees every block and may
  overbook explicitly.
- Times are computed in the clinic's time zone and stored in UTC (Chile has DST; nonexistent local
  times are skipped).

Ceiling: one resource per appointment. Services that need a vet *and* a room at once are a later step.

## Appointments (next stages)

Statuses: `PendingPayment` → `Confirmed` → `Completed` / `Cancelled` / `NoShow`, plus
`NeedsReschedule` when an absence affects a confirmed appointment. Online bookings confirm
automatically (after payment when the service requires a deposit). Staff can cancel, reassign and
overbook. Clients are identified by RUT without an account; an emailed link allows cancellation.
Booking re-checks availability inside a serializable transaction so two people can't take the last slot.

Payments: WebPay Plus (Transbank) for deposits; the CRM never handles card data.
