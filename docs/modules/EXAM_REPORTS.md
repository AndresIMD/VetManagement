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

- A numeric parameter has **bands**: optional species, `Min`/`Max` (both inclusive), label, `Abnormal`.
  The first band containing the value wins (order them; e.g. `≤ 200`, `200 – 399.9`, `≥ 400`).
- A value is **flagged** (`*`) when it falls in an abnormal band, or bands exist for the species and none contains it.
  An unlabelled band is a plain normal range (blood gases).
- Values are kept as typed. For evaluation `86,6` = `86.6`, and analyser bounds `< 1.0` / `>99` count as the bound.
- Automatic interpretation: the band label (one parameter) or `Name: label` lines. A written interpretation replaces it.
- Reference column: the template's text; otherwise all bands when labelled, or the normal range.

## Snapshots

Saving a report copies the title, blocks and every line (name, unit, reference, band, flag) from the template, so
editing a template never changes a report already handed out. "Correct" re-evaluates against the current template.
Patient and owner data are read live (a corrected pet name shows on reprint); age is computed at the reception date.

## Printing and sending

The page renders the sheet in HTML at paper size; **Print / Save PDF** prints only the sheet through a hidden frame
(`wwwroot/js/print.js`), with "Título - Mascota - fecha" as the suggested PDF name. Sending = save as PDF and attach.
Printed labels are Spanish (client-facing); staff screens are English, like the rest of the CRM.

## Limits

- No matrix interpretations yet (e.g. cortisol 3 samples, 4 h × 8 h): write the interpretation by hand.
- No server-side PDF, so the system can't email the report itself.
- One page per report; very long panels (20+ parameters) need a smaller font or a second page (not built).
- ponytail: the list loads the clinic's reports to keep the newest 200; a paged query when a clinic has many thousands.
