# Scheduling (agenda) — design

Principle: **every business policy is per-clinic configuration**, never a constant in code. A clinic's
admin edits it in the staff web before activating the agenda (`Enabled = false` by default).

## Configuration

One versioned settings document per clinic, stored in the database (`ClinicSettings`, key
`scheduling`). On first use it is created from `scheduling.defaults.json` (placeholder values), then
the admin edits it. Every change is validated, versioned (optimistic concurrency) and audited.

| Section | Settings |
|---|---|
| General | `Enabled`, `TimeZone` (IANA, default `America/Santiago`), `ClinicName` (required), `ClinicPhone` (shown in client emails) |
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

## Appointments

Statuses: `PendingPayment` → `Confirmed` → `Completed` / `Cancelled` / `NoShow`, plus
`NeedsReschedule` when an absence affects a confirmed appointment. Online bookings confirm
automatically (after payment when the service requires a deposit). Staff can cancel, reassign and
overbook. Clients are identified by RUT without an account; an emailed link allows cancellation.
Booking re-checks availability while holding an exclusive per-resource lock (`sp_getapplock`) inside a
transaction, so two people can't take the last slot. Serializable isolation alone deadlocked under
contention; `ConcurrentBookingTests` runs parallel bookings against real SQL Server (locally via LocalDB,
and in CI via a SQL Server service container).

Client emails (Spanish, clinic time zone): confirmation on booking, notice when staff moves or cancels,
and a reminder sent once by a background job; each one is toggled in the settings and a failed send never
fails the booking. Tests use a recording sender, so no real email leaves the test run.

Payments: WebPay Plus (Transbank) for deposits; the CRM never handles card data. The provider and its
credentials are deployment configuration per clinic (`IPaymentGateway`: WebPay Plus, or a simulated
gateway outside Production). See [../DEPLOYMENT.md](../DEPLOYMENT.md).

## Online booking (public)

Hosted portal (`VetManagement.Booking.Web`), like WebPay: the clinic's site links to it with
`?returnUrl=`, the client books, pays if needed, and goes back to the site. The return URL must be
in `BookingPortal:AllowedReturnOrigins`.

- Anonymous endpoints under `api/public/booking` (`info`, `availability`, `book`, `{token}`,
  `{token}/cancel`, `payment-return`), rate-limited per IP.
- Client identified by a valid Chilean RUT (modulo 11). Unknown clients and pets are created
  automatically; existing ones are matched by normalized RUT and pet name.
- No deposit → `Confirmed`. Deposit → `PendingPayment` held for `Payment.PendingPaymentHoldMinutes`; the background
  job releases expired holds. A payment arriving after the hold expired is refunded if the slot was taken.
- Client cancellation before the deadline applies the clinic's refund mode.
- The emailed link (`/booking/{publicToken}`) shows status and allows cancellation.
