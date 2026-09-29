# Core GREEN 001 — separate-book catalog

Date: 2026-09-13

The first catalog behavior is implemented with two catalog books and their
book-specific accounts. Welfare has member advance liability `2000` and fee
due-to-association `2200`; association has fee due-from-welfare `1300` and the
sole income account `4000` for the 4% welfare deduction. Catalog creation is
idempotent and uses one ambient EF transaction when the caller already owns
one, otherwise one owned transaction.

Command:

```powershell
$env:MSBUILDDISABLENODEREUSE='1'
& 'C:\Users\mooha\.dotnet\dotnet.exe' test 'tests\ChapanakitCare.Domain.Tests\ChapanakitCare.Domain.Tests.csproj' --filter 'FullyQualifiedName~AccountingCoreTests.Catalog_separates_welfare_from_association_and_only_exposes_the_4_percent_income_account' -m:1 /nodeReuse:false /p:UseSharedCompilation=false --no-restore
```

Observed result: **PASSED**, 1 passed / 0 failed. This is focused evidence
only; posting, periods, immutability, and migration verification remain
unfinished.
