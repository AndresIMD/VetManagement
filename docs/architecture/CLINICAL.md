# Clinical record (ficha clínica) — design

## Visits

A medical visit (`MedicalVisit`) holds the clinical notes: reason, anamnesis, physical examination,
diagnosis, treatment, weight and temperature, plus the procedures it already had. `AppointmentId`
links it to the agenda when it is opened from an appointment (Agenda → clinical record icon). Saving a
visit with a weight updates the pet's current weight.

The pet's file (`/clinical/pets/{petId}`, API `GET api/clinical/pets/{petId}/history`) shows the visits
as a timeline and the preventive care below.

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

Prescriptions as a printable document, attachments (images, lab PDFs), drugs used during a visit
taken out of inventory (F10), WhatsApp reminders.
