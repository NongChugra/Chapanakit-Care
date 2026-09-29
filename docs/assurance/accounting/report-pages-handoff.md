# Accounting report pages handoff

Status at 2026-09-23: implementation complete; focused GREEN verification is
pending the serialized .NET build slot held by the parent.

## Ownership

This bounded work owns only:

- `src/ChapanakitCare.Web/Pages/Accounting/Reports.cshtml`
- `src/ChapanakitCare.Web/Pages/Accounting/Reports.cshtml.cs`
- `tests/ChapanakitCare.Domain.Tests/AccountingReportPageTests.cs`
- this handoff and report-page verification logs.

## Behavior

`ReportsModel` is a read-only Razor Page model over the existing
`AccountingReportQueryService` and `AccountingReportDocuments` contracts. It
accepts the explicit report keys `trialbalance`, `journal`, `generalLedger`,
`incomeexpense`, `financialposition`, `memberbalances`, and
`beneficiaryunpaid`. Book keys are explicitly `welfare` and `association`;
unknown values return the page with a validation error and never fall back to a
different book. Member and beneficiary control reports are explicitly limited
to the welfare book because their query API is welfare-scoped.

All page dates are Thai Buddhist strings parsed by `ThaiBuddhistDate.Parse`
before being passed to the query service as Gregorian `DateOnly` values.
Period reports use `From` and `To`; cutoff reports use `AsOf`. General-ledger
account options are loaded with `AsNoTracking` from the selected book only,
and the query service validates the submitted account against that same book.
The page maps each typed report to the existing document data contract for its
HTML table and uses the existing renderer for PDF and CSV downloads. No page
handler creates, updates, or deletes database rows.

## TDD evidence

The parent’s serialized combined RED run is recorded in
`combined-red-0922.log`. The report page cases compiled and reached the
intentional `ReportsModel.OnGetAsync` `NotImplementedException` stub:

- selected-book trial balance and business-date cutoff;
- selected-book general ledger, account scoping, and Buddhist period parsing;
- invalid book/date rejection without fallback;
- PDF and CSV response formats.

The focused GREEN command to run after the shared build slot is released is:

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
& 'C:\Users\mooha\.dotnet\dotnet.exe' test tests\ChapanakitCare.Domain.Tests\ChapanakitCare.Domain.Tests.csproj --no-restore --filter 'FullyQualifiedName~AccountingReportPageTests' -m:1 /nodeReuse:false /p:UseSharedCompilation=false --logger 'console;verbosity=normal' *> docs\assurance\accounting\report-pages-green-0923.log
```

Do not treat the implementation as verified until that log reports the five
focused executions passing and the parent inspects the resulting diff.
