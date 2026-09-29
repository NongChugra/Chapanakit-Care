# Core RED 001 — separate-book catalog

Date: 2026-09-13

The test names the break it catches: a future change that merges the welfare
prepaid ledger into association operations, or adds earned income outside the
association's 4% welfare deduction account.

Command:

```powershell
$env:MSBUILDDISABLENODEREUSE='1'
& 'C:\Users\mooha\.dotnet\dotnet.exe' test 'tests\ChapanakitCare.Domain.Tests\ChapanakitCare.Domain.Tests.csproj' --filter 'FullyQualifiedName~AccountingCoreTests.Catalog_separates_welfare_from_association_and_only_exposes_the_4_percent_income_account' -m:1 /nodeReuse:false /p:UseSharedCompilation=false --no-restore
```

Observed result: **FAILED**, 0 passed / 1 failed. The test reached the real
SQLite setup and failed at `AccountingSetupService.EnsureCatalogAsync` with
the expected `NotImplementedException` from the compile-ready stub. No
production catalog behavior existed at this point.
