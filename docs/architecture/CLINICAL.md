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
deleting the visit, puts the quantity back ("... removed" / "... deleted" movements). A quantity above
the current stock is rejected. The item name is copied, so the record stays readable if the item changes.

A dose can also name the inventory product applied: one unit leaves stock, and deleting the dose returns it.

Every automatic stock change (sales, supplies, doses) runs under one `sp_getapplock` key (`stock`), so two
of them never overwrite each other's stock count. Manual adjustments in the inventory screens don't take
that lock yet.

## Preventive care (vaccines, deworming)

Each application is a `PreventiveDose`: product, kind, batch, applied date and the date the next dose is
due. Doses of the same series (same protocol, or same product name for one-off products) replace each
other: only the latest one counts as due. Status: `UpToDate`, `DueSoon` (within the reminder window),
`Overdue`, `Superseded`, `NoRepeat`.

## Settings (key `clinical`, versioned, admin only: `clinical.manage`)

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

Records use the existing `medical.read/create/update/delete` permissions; only the settings need
`clinical.manage` (Admin).

## Not yet

Prescriptions as a printable document, attachments (images, lab PDFs), charging the supplies of a visit
in billing, WhatsApp reminders.
