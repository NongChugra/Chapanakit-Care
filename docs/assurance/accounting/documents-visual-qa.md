# Accounting document visual QA

The focused GREEN run generated the durable samples in
`docs/assurance/accounting/document-samples/`. Poppler rendered the pages to
PNG for visual inspection.

| Sample | Pages | Inspection |
| --- | ---: | --- |
| `voucher-original.pdf` | 1 | Thai font, separate book/title/period, document number, prominent `ORIGINAL / ฉบับจริง`, wrapped note and signature labels are legible. The negative amount remains complete and right-aligned. |
| `empty-report.pdf` | 1 | Header stays within the printable top margin; headings remain visible and the empty report message is centered across the table. |
| `long-report.pdf` | 5 | First, middle and last pages show the repeated organization/book/title and table headings; rows 1-120 remain readable, numeric strings are complete, and page numbers run `1 / 5` through `5 / 5`. |

Rendered evidence files are `*-page.png`, `long-report-second-page.png`,
`long-report-p4-high.png`, `long-report-last-page.png`, and the low-resolution
spot-checks `long-report-p3.png`, `long-report-p4.png`, `long-report-p5.png`.
The PDFs contain no logos or vendor identities. The extracted text files are
kept beside each PDF to make the hand-derived row and header assertions
inspectable; the images remain the authority for Thai glyph composition and
layout.

The PDF skill's artifact-operation marker completed successfully once before
the first durable PDF generation. Intermediate build output under `bin/` is
not part of the evidence set.
