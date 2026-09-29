# Core RED 002 — activated balanced welfare posting

Date: 2026-09-13

The test names the break it catches: accepting an unactivated/pre-cutover book,
or recording a member prepaid receipt without balanced welfare lines, frozen
member dimensions, and atomic audit evidence.

Command:

```powershell
$env:MSBUILDDISABLENODEREUSE='1'
& 'C:\Users\mooha\.dotnet\dotnet.exe' test 'tests\ChapanakitCare.Domain.Tests\ChapanakitCare.Domain.Tests.csproj' --filter 'FullyQualifiedName~AccountingCoreTests.Balanced_welfare_posting_records_dimensions_and_audit_without_creating_an_association_journal' -m:1 /nodeReuse:false /p:UseSharedCompilation=false --no-restore
```

Observed result: **FAILED**, 0 passed / 1 failed. The real SQLite test reached
`AccountingSetupService.ActivateBookAsync` and failed with the expected
`NotImplementedException` from the compile-ready stub. No posting was created.
