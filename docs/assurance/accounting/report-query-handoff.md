# Accounting report query handoff

Status at 2026-09-15: **compile-ready contracts and stubs only; no report
behavior is implemented or verified.** This note is intentionally durable for
a usage interruption or computer restart.

## Ownership

This work owns only:

- `src/ChapanakitCare.Infrastructure/AccountingReports/*`
- `tests/ChapanakitCare.Domain.Tests/AccountingReportQueryTests.cs`
- this handoff and report-query test logs.

It must not edit operations, core ledger, persistence, UI, or migration files.

## Public read-only API

`AccountingReportQueryService(AppDbContext)` has these methods:

```csharp
Task<AccountingTrialBalanceReport> GetTrialBalanceAsync(AccountingBookCode bookCode, DateOnly asOfDate, CancellationToken ct = default)
Task<AccountingJournalReport> GetJournalAsync(AccountingBookCode bookCode, AccountingReportPeriod period, CancellationToken ct = default)
Task<AccountingGeneralLedgerReport> GetGeneralLedgerAsync(AccountingBookCode bookCode, string accountCode, AccountingReportPeriod period, CancellationToken ct = default)
Task<AccountingIncomeExpenseReport> GetIncomeExpenseAsync(AccountingBookCode bookCode, AccountingReportPeriod period, CancellationToken ct = default)
Task<AccountingFinancialPositionReport> GetFinancialPositionAsync(AccountingBookCode bookCode, DateOnly asOfDate, CancellationToken ct = default)
Task<AccountingMemberBalancesReport> GetMemberBalancesAsync(DateOnly asOfDate, CancellationToken ct = default)
Task<AccountingBeneficiaryUnpaidReport> GetBeneficiaryUnpaidAsync(DateOnly asOfDate, CancellationToken ct = default)
```

All inputs are Gregorian `DateOnly`. Presentation conversion to Buddhist Era
belongs to the existing `AccountingDocumentRenderer` boundary. Queries must
use `AsNoTracking` and never create, update, or delete records.

The model records deliberately retain raw integer-satang debit/credit values.
`AccountingGeneralLedgerReport` carries debit/credit opening, movement and
closing sides so an abnormal balance cannot be concealed by a display-only
normal-balance conversion.

## Required accounting behavior

- A journal is included once when its **business date** is within the report
  cutoff or period; `RecordedAtUtc` is not the accounting-date filter.
- Reversals remain posted journals. Include both original and reversal lines;
  their opposing debit/credit sides produce the correct signed net balance.
  Do not omit originals merely because `ReversesJournalId` is populated.
- Every query selects the requested book's account/journal IDs. Welfare and
  association balances must never be combined.
- Trial balance returns each in-book account's debit/credit balance through
  cutoff and totals both sides.
- General ledger starts from all account lines before `period.From`, then
  adds lines through `period.To` in business-date / journal-number / line-no
  order. Its opening plus movements must equal closing.
- Income/expense uses only accounts typed `Income` or `Expense`; the valid
  catalog exposes association account `4000` as the only income account.
  Welfare can return an empty zero income/expense report.
- Financial position through cutoff exposes asset, liability and equity
  balances plus cumulative income less expense as current result, and reports
  whether assets equal liabilities + equity + current result.
- Member balances use welfare control accounts `2000` (credit advances) and
  `1200` (debit shortfalls), grouped by `MemberId` dimensions. Preserve a
  frozen `PartySnapshot` fallback if a member cannot be loaded.
- Beneficiary unpaid balances use welfare `2100`, grouped by `DeathCaseId` and
  beneficiary slot: credit is entitlement/payable, debit is paid, and unpaid
  is credit minus debit. Preserve frozen party data.

## TDD status and next exact step

The current test file contains real SQLite numerical examples. The earlier
`report-query-red-001.log` was a transient reflection/existence test and is
not behavioral evidence; that test was removed after it demonstrated the
initially missing boundary. Do not count it as a TDD RED for the query
behavior.

2026-09-20: `report-query-red-002.log` observed the first genuine behavioral
RED: `Trial_balance_uses_business_date_cutoff_keeps_books_separate_and_does_not_mutate_journals`
failed only at `GetTrialBalanceAsync`'s `NotImplementedException`, after the
real SQLite fixture posted both books successfully. A minimal, read-only
trial-balance implementation is now present but has not yet been run GREEN;
the shared build slot was released to the parent immediately after the RED.

The next permitted build is the focused trial-balance GREEN once the shared
build slot is released by the parent:

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
& 'C:\Users\mooha\.dotnet\dotnet.exe' test tests\ChapanakitCare.Domain.Tests\ChapanakitCare.Domain.Tests.csproj --no-restore --filter 'FullyQualifiedName~AccountingReportQueryTests.Trial_balance_uses_business_date_cutoff' -m:1 /nodeReuse:false /p:UseSharedCompilation=false --logger 'console;verbosity=normal' *> docs\assurance\accounting\report-query-green-002.log
```

It should pass before any other report behavior is implemented. Then run each
remaining focused behavior test in sequence: observe its stub RED, implement
only that behavior, and capture its GREEN evidence. Build commands are
serialized across workers; do not run them while another worker holds the
slot.

## Hand-checked test oracle

The fixture seeds both activated books using the real posting service:

| Event | Welfare effect | Association effect |
| --- | ---: | ---: |
| opening 2026-09-01 | cash Dr 27,000; member advance Cr 27,000 | cash Dr 100; opening fund Cr 100 |
| receipt 2026-09-10 | cash Dr 451; advance Cr 451 | due-from Dr 100; 4% income Cr 100 |
| receipt 2026-09-11 | cash Dr 900; advance Cr 900 | operating expense Dr 60; cash Cr 60 |
| reversal 2026-09-12 | advance Dr 900; cash Cr 900 | — |
| benefit accrual 2026-09-13 | advance Dr 3,000; payable Cr 3,000 | — |
| beneficiary payout 2026-09-14 | payable Dr 1,200; cash Cr 1,200 | — |

Expected numerical assertions:

- welfare trial balance at 2026-09-11: cash debit 28,351; member advance
  credit 28,351; no account 4000; both totals 28,351;
- welfare cash ledger 2026-09-10..12: opening debit 27,000, movement debit
  1,351 / credit 900, closing debit 27,451;
- association income/expense to 2026-09-11: income 100 (4000), expense 60
  (5000), net result 40; welfare report zero;
- association financial position: cash 40 + due-from 100 = assets 140;
  opening fund 100 + current result 40 = 140;
- welfare member advance at 2026-09-12: 27,451; beneficiary payable 3,000,
  paid 1,200, unpaid 1,800 at 2026-09-14.

## Known gaps

- All service methods currently throw `NotImplementedException`.
- `AccountingReportDocuments` mappers and PDF/CSV wrapper methods have not
  been added. Add them after numerical query behavior is GREEN, with their
  own focused document-data tests; use existing `AccountingDocumentRenderer`.
- No UI registration, database migration, or final verification belongs to
  this bounded work item.
