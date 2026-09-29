# Accounting core handoff

Status as of 2026-09-20: `core-red-0920.log` observed the real ambient
transaction failure: an invalid association half left its welfare journal
persisted when the caller committed. `core-green-0920-linked-savepoint.log`
then passed after `PostLinkedAsync` began a SQLite savepoint for a caller-owned
transaction and rolled the savepoint back on a later posting failure. The
caller can now commit safely with no half-linked journal, and the next
successful operation starts at `W-00000001` / `A-00000001`.

`core-red-0920-tracker.log` then showed the genuine EF recovery failure: the
first `SaveChangesAsync` marked a caller-owned member edit unchanged even
though the savepoint later rolled it back. `core-green-0920-tracker.log` proves
that recovery now restores the entry's values and modified flags, detaches only
accounting entities introduced by the failed post, restores the book counters,
and lets the caller save its own member change in the same transaction. Period
setup, account setup, reversal, append-only SQLite guards, and accounting
migration/preservation checks remain incomplete.

Compile-ready, unrun core tests now cover: book-scoped additional bank accounts
and association-only expense categories, a closed period blocking a posting
until a documented reopen, a regular journal reversal with replay/one-reversal
protection, and rejection of opening or linked-accrual single-book reversals.
They must be observed RED as a batch before their stub implementations are
written; the shared build slot is currently owned by the parent.

Historical status as of 2026-09-15: catalog, activation, ordinary balanced posting,
positive-amount validation, and book-scoped idempotency have focused test
evidence. `core-red-003.log` caught a negative debit paired with a positive
credit reaching SQLite instead of the service guard; the subsequent focused
run in `core-red-004.log` passed that test. The same run proved the
book-scoped-token behavior was RED, and `core-red-005.log` then showed it
green while the next linked-posting test was RED at the compile-ready stub.

`PostLinkedAsync` is now GREEN: `core-parent-red-0915.log` contains the
focused result, with all five `AccountingCoreTests` passing. It validates one
welfare and one association request, detects a partial or changed replay as a
conflict, and posts both inside one owned or ambient EF transaction. The
linked-operation ID is recorded on both journal headers and is used as their
shared operation/audit ID.

The linked rollback and tracked-state recovery tests described above are
complete. The remaining period, account, reversal, EF/SQLite append-only
integrity, migration, and preservation tests are incomplete.

## Book boundary

`AccountingBookCode.Welfare` and `AccountingBookCode.Association` are separate
books. An `AccountingJournal` and all of its `AccountingJournalLine` rows use
one book only. A linked operation creates one journal per book, never
cross-book lines.

Catalog accounts seeded by `AccountingSetupService.EnsureCatalogAsync`:

| Book | Account code | Role |
|---|---:|---|
| welfare | 1000 | cash |
| welfare | 1100 | configurable bank control (additional bank accounts are 11xx) |
| welfare | 1200 | member contribution shortfall receivable |
| welfare | 2000 | member advances liability |
| welfare | 2100 | welfare benefits payable |
| welfare | 2200 | fee due to association |
| welfare | 3000 | explicit opening accumulated fund |
| association | 1000 | cash |
| association | 1100 | configurable bank control (additional bank accounts are 11xx) |
| association | 1300 | fee due from welfare |
| association | 3000 | explicit opening accumulated fund |
| association | 4000 | 4% welfare deduction income — the only income account |
| association | 5000 | operating expenses control (categories are 50xx) |

Do not use `4000` for cash receipts or fee remittances. Fee accrual recognizes
income once. Remittance clears `2200` / `1300` and moves book-specific bank
cash.

## Entities and dimensions

The core provides the following entities under
`ChapanakitCare.Domain.Entities`. The exact `AppDbContext` sets are
`AccountingBooks`, `AccountingAccounts`, `AccountingPeriods`,
`AccountingJournals`, `AccountingJournalLines`, and
`AccountingPostingAudits`:

* `AccountingBook` — book setup and activation/cutover boundary.
* `AccountingAccount` — immutable-in-use book-specific account catalog,
  account type, role, normal balance and optional bank/cash metadata.
* `AccountingPeriod` — book-specific open/closed period.
* `AccountingJournal` — append-only posted voucher header, request token and
  fingerprint, source/reversal references, business/recorded dates and audit
  identity.
* `AccountingJournalLine` — positive debit or credit only, account plus
  optional `MemberId`, `DeathCaseId`, `BeneficiarySlotNo`,
  `CollectionRequestId`, immutable `PartySnapshot` and `DescriptionSnapshot`.
* `AccountingPostingAudit` — immutable action evidence saved with every
  successful posting.

The generic core deliberately does **not** require existing members/deaths to
exist for every dimension yet. It preserves identifiers and immutable
snapshots so the operation layer can use frozen death/beneficiary data. It
will validate a supplied beneficiary slot is 1 or 2.

## Posting API planned for the operation layer

```csharp
public interface IAccountingPostingService
{
    Task<AccountingPostingResult> PostAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountingLinkedPostingResult> PostLinkedAsync(
        AccountingLinkedPostRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountingPostingResult> ReverseAsync(
        AccountingReverseRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AccountingPostRequest(
    AccountingBookCode BookCode,
    string VoucherType,
    string Description,
    DateOnly BusinessDate,
    string RequestToken,
    string RequestFingerprint,
    AccountingActor Actor,
    IReadOnlyList<AccountingPostLine> Lines,
    AccountingSourceReference? Source = null,
    Guid? ReversesJournalId = null);

public sealed record AccountingPostLine(
    string AccountCode,
    long DebitSatang,
    long CreditSatang,
    AccountingDimensions? Dimensions = null);
```

`PostLinkedAsync` accepts exactly two `AccountingPostRequest` values, one for
each book, and commits both in the caller's active transaction when present;
otherwise it owns a single transaction. It uses one shared operation ID and
does not open nested independent transactions. `PostAsync` follows the same
transaction rule. Idempotent replay returns the original result only if both
token and fingerprint match; the same token with a changed fingerprint is
rejected.

`AccountingSetupService` exposes `EnsureCatalogAsync`, `AddBankAccountAsync`,
`AddExpenseCategoryAsync`, `ActivateBookAsync`, `ClosePeriodAsync` and
`ReopenPeriodAsync`. `ActivateBookAsync` requires an explicit Gregorian
cutover date and opening evidence, and it supports documented zero-opening
activation. For a nonzero opening the operation layer calls it and posts the
opening journal in the same ambient EF transaction. At this checkpoint only
`EnsureCatalogAsync` is implemented; the other setup methods remain stubs.

`AccountingDimensions` exposes `MemberId`, `DeathCaseId`,
`BeneficiarySlotNo`, `CollectionRequestId`, `PartySnapshot`, and
`DescriptionSnapshot`. Keep meaningful frozen descriptions in the operation
layer rather than relying on live member names.

## What the operation layer should prepare

1. It owns receipt, collection request, death accrual/payout, expense,
   transfer, fee remittance and reversal workflow services.
2. It supplies a deterministic payload fingerprint and a user-unique request
   token per mutation attempt.
3. It runs bookkeeping calls inside its existing EF transaction if it has one;
   the core detects and reuses it.
4. It creates fee accrual/remittance through `PostLinkedAsync`; do not create
   a cross-book line or recognize association income during remittance.
5. It must not derive historical paid balances from advance-unit counters.

## Current limits

The first core checkpoint covers catalog, immutable balanced journal posting,
idempotency, audit evidence, book boundaries and period guards. The operation
layer will implement the explicit opening balance operation/cutover workflow
against the core API after this contract is verified.
