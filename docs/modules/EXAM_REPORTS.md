# Exam reports (printable lab results) — design

Staff web → Exams → **Lab Reports** (issue, view, print), **Report Design** (branding) and, in the exam catalog,
**Report template** per exam. API: `api/exam-reports`.

## How it fits together

| Piece | Where | Who edits it |
|---|---|---|
| Branding: paper (Letter/A4), margins, font, two bar colours and the section titles' one (the patient bar's until chosen), header lines, footer (a paragraph: each line as written, an empty line for a gap, `**bold**`), disclaimer, text sizes, whether later pages repeat the pet and owner data (on by default), background strength and area (whole page or results box), background stretched or keeping its proportions (fit to width/height, aligned start/centre/end), logo height (and width when its proportions aren't kept) | settings document `exam-reports` (`ExamReportSettings`) | `exams.update` |
| Logo and background images (one each, PNG/JPEG/WebP, ≤ 3 MB) | table `ClinicImages` | `exams.update` |
| Template of a catalog exam: title, printed blocks, parameters with unit, normal ranges, reference column, bar, options, interpretation bands; interpretation rules | same settings document, `Templates[]` keyed by `ExamId` | `exams.update` |
| Issued report: registered pet or typed patient, exam, reception date, referring vet, optional lot number, values, interpretation | table `ExamReports` | `examsperformed.create` / `.update` / `.delete` |

Printed blocks (per template): patient and owner data, interpretation, method (catalog brand – machine),
issued by, ↑ / ↓ arrows, previous result column, criteria table. The title and the results table always print.
Under the results come the legend, then the interpretation, the criteria table and the signature, then the method bar
at the foot of the box. Per template (`InterpretationAtFoot`, off by default), the interpretation, table and signature
sit at the foot too, just above the method bar, leaving the space under the results empty. The criteria table keeps at
least 5 mm above it (what the page breaks count) and, on a page with room to spare, up to 10 mm above and 6 mm below.
The template editor's preview shows a placeholder interpretation until a sample value gives one.
Asked in the form (per template): the lot number, so the form only shows the fields an exam needs.

## Interpretation rules

- A numeric parameter has a **normal range** (`Ranges`: min/max, both inclusive, either open; one for all species
  and/or one per species, the species' own one wins). A value outside it prints in red, with ↓ / ↑ unless the template
  turns the arrows off (`Sections.DirectionArrows`).
- Optional **interpretation bands** name what a value means (label, optional species, `Abnormal` flag): the first band
  containing the value wins (order them; e.g. `≤ 200`, `200 – 399.9`, `≥ 400`). A value in an `Abnormal` band prints
  in red with `*`, even inside the normal range.
  Without a normal range, a value outside every band is flagged too.
- Values are kept as typed. For evaluation `86,6` = `86.6`. An analyser's bound (`< 1.0`, `>1000`, `≤ 5`) counts only
  where it settles the question: `< 1.0` is low against a range starting at 1.0, falls in the band `≤ 200` and meets
  `8 h < 1.4`, but not `8 h ≥ 1.0`; its bar mark sits on the bound. A value that isn't a number (e.g. "Not measurable",
  for an analyser that shows `...`, `/` or `-`) prints as typed, is never flagged and draws no bar. In the form, the
  menu next to each number value types "Not measurable" (Spanish: "No medible") or puts `>` / `<` before the number.
- **Text parameters** (Positivo / Negativo, DEA 1, blood type) may list options; options marked `AbnormalOptions`
  (e.g. "Positivo") are flagged. A report whose results are all text prints each one highlighted (name and a result
  pill: solid red when flagged, outlined in the result colour otherwise) instead of the table, without unit, reference
  or legend.
- **Interpretation rules** read several values together (e.g. cortisol at 0, 4 and 8 h). A rule is a meaning plus
  conditions that must all hold; a condition compares a number parameter with a number or with a percentage of another
  parameter ("4 h < 50 % of basal"), and may have alternatives (`Or`, the editor's "or…"): it holds when its comparison
  or any alternative does, e.g. "4 h < 1.4 or 4 h < 50 % of basal" and "8 h > 1.4" and "8 h < 50 % of basal" →
  pituitary-dependent. A missing or non-numeric value makes its comparison false, and so does a bound that doesn't
  settle it. Rules are checked in order and the first match wins.
- Automatic interpretation: the first matching rule; otherwise the band label (one parameter) or `Name: label` lines.
  A written interpretation replaces it.
- Reference column, chosen per parameter: **Normal range** ("3.5 - 5.8"; without a range, the labelled bands, in a row
  split by " / " or, with `BandsOnePerLine`, one under the other), **Written text** (e.g. "Sin rango de referencia"; its
  line breaks print) or **Nothing**. With a range of both limits, **Show bar** draws the
  value IDEXX-style (25 mm) in a column of its own next to the range, both under one "Valor referencial" header: the
  normal range is the middle third, a value beyond the bar shows as an arrow at its edge. A bar is one line of text tall,
  so a line with a bar takes no more room than one without; a reference without a bar writes across both columns.
- The table's columns follow the report's texts (`ReportColumns`): each is as wide as its widest text (never narrower
  than its header) and the room left is shared out so the space between the texts of any two neighbouring columns is
  the same, whether the names are short (blood gases) or long (a blood count). Text widths are estimated per font
  (figures and letters measured against Segoe UI). When the texts don't fit, the parameter names wrap first; names wrap
  past 40 % of the width and written references past 45 %.
- Report Design → "Result columns" (`ColumnAlign`): the columns after the parameter names are **centered** (default, also
  for designs saved before the setting) or **aligned left**; the names always align left.
- Exams read without a reference (e.g. cortisol samples judged together by the rules): set each parameter's
  reference to **Nothing**; when no line has a reference or bar, the "Valor referencial" column is dropped.
- The legend under the table explains only the marks the report prints.
- **Criteria table** (per template, optional): a grid copied from the exam's instructions (e.g. the manufacturer's
  dexamethasone suppression table: 4 h, 8 h, interpretation), up to 8 columns and 20 rows, printed as written under the
  results on the last page. It is for the reader; the automatic interpretation still comes from the rules (a row's
  "or" is a condition's alternative). A report keeps a copy, like the rest of the template. Its headings (`Headers`, in
  the design's header colour) go in the first row (default, also for reports issued before the setting), the first
  column, both (a two-way table, e.g. ranges by age across the top and by weight down the side) or nowhere; the
  editor's two ticks work like Excel's "Header row" and "First column", and a first row that isn't a heading prints as
  an ordinary row.
- Parameter codes are generated from the first name and hidden; renaming a parameter keeps its code.
- **Section titles** (`Kind: Section`) group the parameters after them, up to the next title; a report prints a title
  as a band across the results box above its first result, in the section colour of Report Design (dark text on a light
  colour, white otherwise), a notch smaller than the main bars (text 0.9 of the results', at most 10.5 pt, and a
  slimmer band), and leaves it out when none of its parameters has a value. Parameters and titles are moved
  up and down in the editor, which is also how a parameter changes section.
- **Page breaks** (`Kind: PageBreak`) print nothing: the lines after one start a new page, and a section title right
  above it goes along. A report keeps one only between values: never first or last, and several in a row count once.
  The report form doesn't show them.

## Snapshots

Saving a report copies the title, blocks and every line (name, unit, reference, band, flag, direction, bar range) from
the template, so editing a template never changes a report already handed out. "Correct" re-evaluates against the
current template. **Update format** (report page, `POST api/exam-reports/{id}/renew`, a correction) does the same without
the form: the exam's current template and catalog (title, printed blocks, names, units, ranges and flags, bars,
sections, method) are applied to the report's own data, while the values as typed, patient, reception and creation
dates, referring vet, lot, a written interpretation and what it took from inventory stay; it is refused, leaving the
report as it was, when a value's parameter is no longer in the template. The look (colours, font, column alignment,
bars' size) needs no update: it comes from Report Design every time a report is drawn. A registered pet's patient and
owner data are read live (a corrected pet name shows on reprint); age is computed at the reception date.

**Typed patients.** Patients aren't registered yet, so a report can be issued for a patient typed in the form (name,
species, breed, sex, birth date, owner). The report keeps it as typed (`Patient`) and prints it so; it is also
linked to a **provisional** client and pet (`IsProvisional`), created on the first report and reused when the same
patient (name, species, owner first and last name) is typed again, so its reports share a history. A query filter hides
provisional clients and pets everywhere (client lists and searches, pets, clinical record, sales); only the exam report
repositories read them (`*WithProvisional`, `GetProvisionalAsync`), and the API never returns their `PetId`. The form's
"Typed before?" search fills the fields from the patient's last report. **Register as client** (report page, needs
`clientspets.create`) turns them into a normal client and pet, clears `Patient` on their reports (which then show the
live client data) and opens the client screen to complete tax ID, phone and email. Reports typed before provisional
clients existed stay unlinked. **New report for this patient** (report page and list) opens the form with the
patient, referring vet and reception date of that report, on another exam. Its species picks the normal ranges like a registered pet's. The form links age (years, months) and birth date: typing
an age (the usual case, "5 years 6 months") works the birth date out back from the reception date and flags it
`BirthdateApproximate`; picking an exact date shows the age it gives. A correction shows both again.
Typed data (patient, breed, owner, referring vet, lot number) is stored in capitals, and the patient block always prints in capitals.

With **Previous result column** on, saving also copies the values of the pet's latest report of the same exam received
earlier (`PreviousReceivedOn`, `PreviousValue` per line, matched by parameter code). The column is the last one, headed
by that date, and only prints when such a report exists (a typed patient's history is its provisional pet's). Being a copy, correcting the older report doesn't change this one;
correcting this one looks the previous report up again.

## Printing and sending

The page renders the report as pages of the chosen paper size, in the chosen font (`FontFamily`: one of the fonts
every Windows PC has, or one shipped with the app and embedded when printing: Inter and Figtree, close to Aptos, and Jost,
close to Futura; all SIL Open Font License, in `wwwroot/fonts`). Lines that don't fit go to the next page, which repeats
the header, the patient bar with the patient, owner and reception date (unless Report Design turns it off: "Pet and
owner data on the following pages"), and the footer; every page says "Página X de N".
Pages are laid out from the heights measured on the rendered sheet (`report-sheet.js`, `ReportPagination`): a section
title never stays alone at the foot of a page, a page break of the template starts a new page, and the closing blocks (legend, interpretation, criteria table, signature, method) go
on the last page, taking the last two lines with them when they don't fit. The
**lot number** (test kit or reagent; asked only when the template says so) prints in the method bar ("Vcheck - BIONOTE · LOTE: A123"), alone if the
method is hidden.
Pagination estimates row heights from text length, on the generous side: a page may end a row early. ponytail: an
estimate, not a measurement; an unusually long cell could still overflow a page (check the preview), measure in the
browser if that happens.
**Print / Save PDF** prints only the pages through a hidden frame (`wwwroot/js/print.js`); the suggested file name is
`INFORME-EXAMEN_MASCOTA-APELLIDO_dd-MM-aaaa` (Chrome names the PDF after the top page, so the page borrows that title
while the dialog is open). Sending = save as PDF and attach. The frame fixes the paper with
`@page { size: <width> <height>; margin: 0 }`, so the dialog can't use another default paper (legal/"oficio") and
split each sheet, and each page prints a hair shorter than the paper so rounding never adds a page. Use the button,
not the browser's own Ctrl+P, which prints the whole screen.

Every change is audited (who, when, what): reports (create; correct with before/after; delete), logo and background,
and the design document with every template (`ExamReportSettings`), like the exam catalog, requests and external labs.
Printed labels are Spanish (client-facing); staff screens are English, like the rest of the CRM.

## Inventory (the exam's test kit)

A catalog exam can name the inventory product one exam uses up (`Exam.ItemId`, e.g. its test kit) and how many units
(`ItemQuantity`, default 1); the catalog form shows the field only with the Inventory module, through a narrow search
(`api/exams/stock-items`: name and the daily-use warehouse's units) so a lab supervisor needs no inventory permissions
to link it.

Some species can have a product of their own (`Exam.SpeciesItems`: species, product, units; e.g. the canine and feline
pancreatic lipase kits are different products): a report of that species (the typed patient's or the registered pet's)
takes it, the other species take `ItemId`, and with no `ItemId` they take nothing. One per species, never "Unknown";
kept as JSON with the exam, by id like `ItemId`.

- **Deduct** (report page, "Deduct from inventory"; `POST api/exam-reports/{id}/stock`, whoever makes reports):
  under the clinic-wide `stock` lock, the report must not have deducted yet (two clicks or two desks take it once) and
  the daily-use warehouse must cover the quantity ([warehouses](INVENTORY.md); the page names the warehouses to ask); the product leaves stock with the movement "Exam report #N – PET SURNAME" (pet
  and owner's surname as in the PDF name, typed patients as typed) and the report keeps what it took: product, name,
  quantity, when and who (`StockItemId`…).
- **Return** ("Return to inventory"; `DELETE api/exam-reports/{id}/stock`, a correction: Laboratory) puts back what was
  taken ("… (returned)"); the report can deduct again. Deleting a report puts it back too ("… (deleted)"), in the same
  transaction. What goes back is what the report took, whatever the catalog or a correction says since.
- With the Inventory module off the endpoints answer 404, reports show nothing about stock and saving an exam keeps its
  links. A product deleted from inventory counts as no link; returning to a deleted product clears the report only.

## Limits

- A rule joins its conditions with "and" and a condition's alternatives with "or"; any other shape, e.g. "(A and B) or
  C", is written as two rules with the same meaning.
- No server-side PDF, so the system can't email the report itself.
- ponytail: the list loads the clinic's reports to keep the newest 200; a paged query when a clinic has many thousands.
