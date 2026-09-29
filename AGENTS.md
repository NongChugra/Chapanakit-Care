# Chapanakit Care repository guidance

- The Obsidian restart plan is the product scope authority.
- Store money as integer satang and percentages as integer basis points.
- Store business dates as Gregorian `DateOnly`; convert to Buddhist Era only at display/export boundaries.
- SQLite is the single-PC source of truth. Excel files are read-only import evidence, never a live database.
- Death confirmation, snapshots, calculation, member status, and counter ledger changes must be one atomic transaction.
- Append-only histories and confirmed snapshots are not edited in place.
- Use test-driven development for production behavior changes.
- Do not implement login, install-lock, payment/receipt/accounting, or production workbook migration in the first-day checkpoint.

## Active accounting phase (authorized 2026-09-12)

- Accounting/reporting is now authorized as work after the first-day checkpoint. The association's only earned income is the 4% welfare deduction; member advances are not income.
- Member prepaid/welfare accounting and association operating accounting are separate books. Record 4% fee accrual and actual remittance separately with reciprocal balances; do not count income twice.
- For `continue` or a restart, read `docs/assurance/accounting/PROGRESS.md` and resume the earliest incomplete phase. Save implementation, decisions and verification evidence durably in the repository.
- Preserve all pre-existing workspace changes. Do not infer paid balances from first-day administrative counters or create historical receipts automatically.
