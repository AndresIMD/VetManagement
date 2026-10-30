# Clinical record (ficha clínica) — design

## Visits

A medical visit (`MedicalVisit`) holds the clinical notes: reason, anamnesis, physical examination,
diagnosis, treatment, weight and temperature, plus the procedures it already had. `AppointmentId`
links it to the agenda when it is opened from an appointment (Agenda → clinical record icon). Saving a
visit with a weight updates the pet's current weight.

The pet's file (`/clinical/pets/{petId}`, API `GET api/clinical/pets/{petId}/history`) shows the visits
as a timeline and the preventive care below.

## Supplies used in a visit (inventory)

Each visit lists the drugs and materials used (`VisitSupply`: inventory item, quantity). With
`DeductStockOnUse` (default on) they leave stock with a movement "Visit #id"; removing a supply, or
deleting the visit, puts the quantity back ("... removed" / "... deleted" movements). Supplies and doses use the
daily-use warehouse ([warehouses](INVENTORY.md)): a quantity above its units is rejected, naming the warehouses to ask.
The item name is copied, so the record stays readable if the item changes.

A dose can also name the inventory product applied: one unit leaves stock, and deleting the dose returns it.

Every stock change takes the clinic-wide `stock` lock
([exclusive work](../architecture/ARCHITECTURE.md#shared-mechanisms)), so sales, supplies and doses never overwrite
each other's stock count.

## Preventive care (vaccines, deworming)

Each application is a `PreventiveDose`: product, kind, batch, applied date and the date the next dose is
due. Doses of the same series (same protocol, or same product name for one-off products) replace each
other: only the latest one counts as due. Status: `UpToDate`, `DueSoon` (within the reminder window),
`Overdue`, `Superseded`, `NoRepeat`.

## Settings (key `clinical`, versioned, `clinical.manage`)

| Setting | Default | Meaning |
|---|---|---|
| `Protocols[]` | Antirrábica 365 d, Óctuple/Séxtuple (dog) 365 d, Triple felina (cat) 365 d, desparasitación interna 90 d / externa 30 d | Stable `Code`, `Name`, `Kind`, `Species` (null = any), `IntervalDays` (0 = single dose), `Enabled` |
| `Reminders.Enabled` | `true` | Email owners before a dose is due |
| `Reminders.DaysBefore` | `7` | Reminder lead time, also the "due soon" window |
| `DeductStockOnUse` | `true` | Supplies used in visits and inventory products of doses leave stock |

The default protocols are placeholders; each clinic's vets set their own schedule. Recording a dose
from a protocol fills its name, kind and next due date; the vet can override the product name and date.

## Reminders

The background job (every 5 minutes, with the appointment reminders) emails the owner once per dose
when the next dose is within `DaysBefore` days (or up to 30 days overdue). A failed send is logged and
retried on the next run. The due list (`/clinical/due`) shows every due or overdue dose with the owner's
contact for a call.

## Permissions

Records use the `medical.read/create/update/delete` permissions: the Clinical role reads, creates and updates;
deleting and the settings (`clinical.manage`) need Clinical + Supervisor, or Admin.

Supplies of a visit are charged with "Charge visit" ([BILLING.md](BILLING.md#charge-visit-from-the-clinical-record)).

## Not yet

Prescriptions as a printable document, attachments (images, lab PDFs), WhatsApp reminders.
