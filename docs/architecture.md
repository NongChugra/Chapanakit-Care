# First-day MVP architecture

```text
Windows desktop shell (own taskbar icon)
  WebView2 embedded Razor Pages + JavaScript table/form assistance
                    |
                    v
Application services (single local process)
  members, death, resets, reports, audit, backup
                    |
                    v
EF Core + SQLite beside the executable
  constraints, ordered migrations, transactional writes
```

The application remains a modular monolith because member state, death snapshots, calculations,
advance-unit movements, and audit records must commit atomically. It is local-only: Kestrel binds
to loopback and the desktop shell embeds it through WebView2. The desktop executable owns the
application window and taskbar icon, while the web host is an internal component rather than a
browser-facing application.

## Data boundaries

- `members` is the current editable member record; its run number is immutable.
- `beneficiaries` contains one or two current active equal-share recipients.
- `death_cases`, member/beneficiary snapshots, and `death_calculations` preserve confirmation facts.
- Certificate PDF bytes, metadata, size, and SHA-256 are stored with the confirmed death.
- Advance ledgers/reset batches preserve counter movement; audit logs preserve human-readable history.
- UI preferences store column order, visibility, sort state, and combined-field components.
- Backup runs record verified database-backup creation.

The final package starts without a database. Ordered migrations create the schema on first launch;
old checkpoint databases are not a supported production-migration source for this restart.

Money uses integer satang to avoid floating-point drift. Death confirmation copies snapshots so
later member edits cannot rewrite a confirmed benefit or report. Address and gender automation are
suggestions only. SQLite backup uses the backup API and verifies `integrity_check` before delivery.
