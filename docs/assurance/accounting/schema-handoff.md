# Accounting schema verification checkpoint

2026-09-25 parent recovered and verified prior schema workers' implementation.

- Final migration: `20260924113236_AccountingLedgerIntegrity` and matching Designer/ModelSnapshot.
- Core/accounting collection tables are added without manufacturing historical journals or receipts.
- Composite foreign keys keep journal lines and reversal links in the correct book.
- The existing `CoordinatorWriteGuard` remains the sole SaveChanges override owner and calls the accounting validation guard.
- EF rejects unbalanced/incomplete journal writes and mutations to posted histories.
- SQLite triggers reject update/delete of journals, lines, posting audits, collections, and `accounting.%` AuditEvents.
- `schema-opening-cash-green-0925.log`: all eight AccountingMigrationTests passed, including pre-accounting upgrade preservation, model drift, and raw SQL boundary checks. The same batch passed 20/20 overall.

Earlier `schema-integrity-green-0924.log` was 7/8, despite its filename: the final test used string Guid parameters that matched no row. Typed Guid parameters corrected that test and the parent rerun above passed.

This is focused evidence, not final acceptance. Full regression, publish, backup/restore, and fresh final-strict review are still outstanding. No production database was migrated or activated by the agent.

EF tooling: root `dotnet-tools.json`; use Infrastructure as both project and startup because Web does not reference EF Design. Do not pass MSBuild switches as dotnet-ef options. Serialize shared builds and use the user SDK at `C:/Users/mooha/.dotnet/dotnet.exe`.
