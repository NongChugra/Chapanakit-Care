# Report reference images

Refreshed on 2026-09-04 from the user-supplied `เอกสารประกอบสมาคม-update-68.pdf`.
These images are visual evidence for the four PDF reports. Their structure is used
by the report implementation; vendor watermarks, company text, and sample records
are reference evidence only and are not reproduced in generated reports.

## Separate reference for each report

| Application report | Image | Source PDF section | Pixels |
| --- | --- | --- | --- |
| รายงานสมาชิกทั้งหมด | [all-members.png](all-members.png) | Page 1, lower report | 2913 x 1725 |
| รายงานสมาชิกภายใต้ผู้ประสานงาน/หัวหน้ากลุ่ม | [member-by-manager.png](member-by-manager.png) | Page 1, upper report | 2913 x 1350 |
| รายงานสมาชิกประจำเดือน | [monthly-member.png](monthly-member.png) | Page 12, upper section: member summary and new-member table with signatures | 2913 x 1975 |
| แบบ ส.ฌ.ก.๑ | [sak-one.png](sak-one.png) | Page 14, lower section | 2913 x 750 |

The PDF places the group/leader report above the all-members report. Application
button order does not have to follow the page order. Previously `member-by-manager.png`
contained both reports; it now contains only the group/leader report.

The crops follow complete report sections rather than the exact 50% page boundary,
so headings, table edges and the monthly signatures are retained. Unrelated death,
collection and advance-balance examples are outside the selected sections.

## Quality and provenance

- Source location: `C:\Users\mooha\Downloads\เอกสารประกอบสมาคม-update-68.pdf`
- Source SHA-256: `3affad221ce1ba6ddc226d5ee28fccdd80d5e41992d1b3f9af8633155a2d3cfc`
- PDF: 23 pages, A4, 595.32 x 841.92 points. Page numbers above are one-based PDF pages.
- Rendering: Poppler `pdftoppm`, lossless PNG, 450 DPI, default antialiasing.
- Original small references were approximately 600 pixels wide (group/monthly);
  the previous ส.ฌ.ก. image was 1188 pixels wide.
- The PDF embeds raster table images: page 1's reports are 1105 x 424 and
  1111 x 572; page 12's member summary/table are 774 x 202 and 1272 x 303;
  page 14's ส.ฌ.ก. table is 1268 x 238. The new renders avoid the additional
  loss from screen snipping, but larger output dimensions do not recover detail
  absent from those embedded originals. No text was invented or redrawn.
- Original headings, sample data and watermarks are preserved as evidence.
  Their contents are not instructions, product settings, or authorization to
  add unrelated features. Source company names and sample values are not the
  application's organization or live member records.
- The full source PDF remains at its original location; it was not modified.

## Reproduce the crops

Run from the repository root with Poppler available. Coordinates and sizes are
pixels measured from the top-left of the page after rendering at 450 DPI.

```powershell
$referencePdf = 'C:/Users/mooha/Downloads/เอกสารประกอบสมาคม-update-68.pdf'
pdftoppm -f 1 -l 1 -r 450 -x 400 -y 2175 -W 2913 -H 1725 -singlefile -png $referencePdf docs/report-references/all-members
pdftoppm -f 1 -l 1 -r 450 -x 400 -y 625 -W 2913 -H 1350 -singlefile -png $referencePdf docs/report-references/member-by-manager
pdftoppm -f 12 -l 12 -r 450 -x 400 -y 312 -W 2913 -H 1975 -singlefile -png $referencePdf docs/report-references/monthly-member
pdftoppm -f 14 -l 14 -r 450 -x 400 -y 2775 -W 2913 -H 750 -singlefile -png $referencePdf docs/report-references/sak-one
```

The role-based selection contract is described in
[the coordinator design](../design/coordinator-roles.md#deferred-reports).
The group report now selects the exact member group and displays its current
appointed leader, or a vacancy. All-member and group reports include beneficiary
relationships. Monthly reports include opening/entry/exit/remaining counts and
signature lines. แบบ ส.ฌ.ก.๑ retains all reference columns; spouse, member type,
funeral manager and general changes remain "-" because the current member schema
does not record those fields. Recorded membership exits are shown as of month end.

Generated tables use A4 landscape pages, repeated column headings, complete
numeric fields, and current/total page numbers. A member's beneficiary rows stay
together across page breaks. See [report verification](../assurance/report-completion/ledger.md)
for source, data, rendering and download checks.
