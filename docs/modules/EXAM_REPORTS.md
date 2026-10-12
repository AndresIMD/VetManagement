# Exam reports (printable lab results) — design

Staff web → Exams → **Lab Reports** (issue, view, print), **Report Design** (branding) and, in the exam catalog,
**Report template** per exam. API: `api/exam-reports`.

## How it fits together

| Piece | Where | Who edits it |
|---|---|---|
| Branding: paper (Letter/A4), margins, two bar colours, header lines, footer lines, note, background strength | settings document `exam-reports` (`ExamReportSettings`) | `exams.update` |
| Logo and background images (one each, PNG/JPEG/WebP, ≤ 3 MB) | table `ClinicImages` | `exams.update` |
| Template of a catalog exam: title, printed blocks, parameters with unit, reference text, options, interpretation bands | same settings document, `Templates[]` keyed by `ExamId` | `exams.update` |
| Issued report: pet, exam, reception date, referring vet, values, interpretation | table `ExamReports` | `examsperformed.create` / `.update` / `.delete` |

Printed blocks (per template): patient and owner data, interpretation, method (catalog brand – machine),
issued by. The title and the results table always print.

## Interpretation rules

- A numeric parameter has a **normal range** (`Ranges`: min/max, both inclusive, either open; one for all species
  and/or one per species, the species' own one wins). A value below it prints with ↓, above with ↑, in red.
- Optional **interpretation bands** name what a value means (label, optional species, `Abnormal` flag): the first band
  containing the value wins (order them; e.g. `≤ 200`, `200 – 399.9`, `≥ 400`). An abnormal band prints `*`.
  Without a normal range, a value outside every band is flagged too.
- Values are kept as typed. For evaluation `86,6` = `86.6`, and analyser bounds `< 1.0` / `>99` count as the bound.
- Automatic interpretation: the band label (one parameter) or `Name: label` lines. A written interpretation replaces it.
- Reference column, chosen per parameter: **Normal range** ("3.5 - 5.8"; without a range, the labelled bands),
  **Written text** (e.g. "Sin rango de referencia") or **Nothing**. With a range of both limits, **Show bar** draws the
  value IDEXX-style: the normal range is the middle third, a value beyond the bar shows as an arrow at its edge.
- Parameter codes are generated from the first name and hidden; renaming a parameter keeps its code.

## Snapshots

Saving a report copies the title, blocks and every line (name, unit, reference, band, flag, direction, bar range) from
the template, so editing a template never changes a report already handed out. "Correct" re-evaluates against the
current template. Patient and owner data are read live (a corrected pet name shows on reprint); age is computed at the
reception date.

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

- No matrix interpretations yet (e.g. cortisol 3 samples, 4 h × 8 h): write the interpretation by hand.
- No server-side PDF, so the system can't email the report itself.
- ponytail: the list loads the clinic's reports to keep the newest 200; a paged query when a clinic has many thousands.
