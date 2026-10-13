# Exam reports (printable lab results) — design

Staff web → Exams → **Lab Reports** (issue, view, print), **Report Design** (branding) and, in the exam catalog,
**Report template** per exam. API: `api/exam-reports`.

## How it fits together

| Piece | Where | Who edits it |
|---|---|---|
| Branding: paper (Letter/A4), margins, font, two bar colours, header lines, footer lines, disclaimer, background strength and area (whole page or results box), background stretched or keeping its proportions (fit to width/height, aligned start/centre/end), logo height (and width when its proportions aren't kept) | settings document `exam-reports` (`ExamReportSettings`) | `exams.update` |
| Logo and background images (one each, PNG/JPEG/WebP, ≤ 3 MB) | table `ClinicImages` | `exams.update` |
| Template of a catalog exam: title, printed blocks, parameters with unit, normal ranges, reference column, bar, options, interpretation bands; interpretation rules | same settings document, `Templates[]` keyed by `ExamId` | `exams.update` |
| Issued report: registered pet or typed patient, exam, reception date, referring vet, values, interpretation | table `ExamReports` | `examsperformed.create` / `.update` / `.delete` |

Printed blocks (per template): patient and owner data, interpretation, method (catalog brand – machine),
issued by, ↑ / ↓ arrows, previous result column. The title and the results table always print.

## Interpretation rules

- A numeric parameter has a **normal range** (`Ranges`: min/max, both inclusive, either open; one for all species
  and/or one per species, the species' own one wins). A value outside it prints in red, with ↓ / ↑ unless the template
  turns the arrows off (`Sections.DirectionArrows`).
- Optional **interpretation bands** name what a value means (label, optional species, `Abnormal` flag): the first band
  containing the value wins (order them; e.g. `≤ 200`, `200 – 399.9`, `≥ 400`). A value in an `Abnormal` band prints
  in red with `*`, even inside the normal range.
  Without a normal range, a value outside every band is flagged too.
- Values are kept as typed. For evaluation `86,6` = `86.6`, and analyser bounds `< 1.0` / `>99` count as the bound.
- **Text parameters** (Positivo / Negativo, DEA 1, blood type) may list options; options marked `AbnormalOptions`
  (e.g. "Positivo") are flagged. A report whose results are all text prints each one highlighted (name and a result
  pill: solid red when flagged, outlined in the result colour otherwise) instead of the table, without unit, reference
  or legend.
- **Interpretation rules** read several values together (e.g. cortisol at 0, 4 and 8 h). A rule is a meaning plus
  conditions that must all hold; a condition compares a number parameter with a number or with a percentage of another
  parameter ("4 h < 50 % of basal"). A missing or non-numeric value makes its condition false. Rules are checked in
  order and the first match wins.
- Automatic interpretation: the first matching rule; otherwise the band label (one parameter) or `Name: label` lines.
  A written interpretation replaces it.
- Reference column, chosen per parameter: **Normal range** ("3.5 - 5.8"; without a range, the labelled bands),
  **Written text** (e.g. "Sin rango de referencia") or **Nothing**. With a range of both limits, **Show bar** draws the
  value IDEXX-style under the reference: the normal range is the middle third, a value beyond the bar shows as an arrow
  at its edge.
- The legend under the table explains only the marks the report prints.
- Parameter codes are generated from the first name and hidden; renaming a parameter keeps its code.

## Snapshots

Saving a report copies the title, blocks and every line (name, unit, reference, band, flag, direction, bar range) from
the template, so editing a template never changes a report already handed out. "Correct" re-evaluates against the
current template. A registered pet's patient and owner data are read live (a corrected pet name shows on reprint);
age is computed at the reception date.

**Typed patients.** Patients aren't registered yet, so a report can be issued for a patient typed in the form (name,
species, breed, sex, birth date, owner): it is kept only in the report (`Patient`, `PetId` null) and no client or pet
is created. Its species picks the normal ranges like a registered pet's.

With **Previous result column** on, saving also copies the values of the pet's latest report of the same exam received
earlier (`PreviousReceivedOn`, `PreviousValue` per line, matched by parameter code). The column is the last one, headed
by that date, and only prints when such a report exists; typed patients have no history, so never. Being a copy, correcting the older report doesn't change this one;
correcting this one looks the previous report up again.

## Printing and sending

The page renders the report as pages of the chosen paper size, in the chosen font (`FontFamily`, one of the fonts
every Windows PC has). Lines that don't fit go to the next page, which repeats the header, a line with the patient,
"(continuación)" and the footer; every page says "Página X de N". Interpretation and method go on the last page.
Pagination estimates row heights from text length, on the generous side: a page may end a row early. ponytail: an
estimate, not a measurement; an unusually long cell could still overflow a page (check the preview), measure in the
browser if that happens.
**Print / Save PDF** prints only the pages through a hidden frame (`wwwroot/js/print.js`); the suggested file name is
`INFORME-EXAMEN_MASCOTA-APELLIDO_dd-MM-aaaa`. Sending = save as PDF and attach.
Printed labels are Spanish (client-facing); staff screens are English, like the rest of the CRM.

## Limits

- Rules join conditions with "and" only; an "or" is written as two rules with the same meaning.
- No server-side PDF, so the system can't email the report itself.
- ponytail: the list loads the clinic's reports to keep the newest 200; a paged query when a clinic has many thousands.
