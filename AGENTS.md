# Chapanakit Care repository guidance

- The Obsidian restart plan is the product scope authority.
- Store money as integer satang and percentages as integer basis points.
- Store business dates as Gregorian `DateOnly`; convert to Buddhist Era only at display/export boundaries.
- SQLite is the single-PC source of truth. Excel files are read-only import evidence, never a live database.
- Death confirmation, snapshots, calculation, member status, and counter ledger changes must be one atomic transaction.
- Append-only histories and confirmed snapshots are not edited in place.
- Use test-driven development for production behavior changes.
- Do not implement login, install-lock, payment/receipt/accounting, or production workbook migration in the first-day checkpoint.

