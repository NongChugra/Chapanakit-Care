# Chapanakit Care

Local single-PC welfare-association application for the first-day scope defined on 25/08/69.

## Repository layout

```text
src/      Application, domain, EF Core migrations, and the synthetic fixture
tests/    Unit and real temporary-SQLite integration tests
docs/     Architecture, verification, GitHub handoff, and manual acceptance checklist
tools/    Repeatable developer utilities
.github/  GitHub Actions CI
```

Generated packages, local SDK caches, runtime SQLite data, backups, and test/build output are
intentionally excluded from Git. See [GitHub handoff](docs/github-setup.md) before the first push.

## Run a delivered package

Open `artifacts\final\ChapanakitCare.exe` and keep the complete `final` folder together. The
package contains its own .NET runtime, stores SQLite beside the executable in `App_Data`, listens
only on `127.0.0.1:5188`, and opens the user interface in the default browser.

The launcher provides editable development constants, synthetic demo-member import, demo-data
clearing, and database backup. Demo import is safe when members already exist: new fixture members
are added and fixture members already present are skipped.

Important data behavior:

- Closing and reopening preserves the local database, table-column choices, order, and sorting.
- A confirmed death stores immutable member, beneficiary, settings, and money snapshots.
- A death certificate must be a real PDF of at most 10 MB and remains downloadable.
- **สำรองฐานข้อมูล** downloads a verified SQLite backup. Keep backups outside the app folder.
- **ล้างข้อมูลสมาชิกสาธิต** is destructive demo tooling, not production user management.

For a completely fresh demo, close the launcher and delete only
`artifacts\final\App_Data\chapanakit-care-demo.db`. The next launch creates an empty migrated
database. The delivered folder intentionally contains no runtime database or previous user data.

## Implemented first-day surface

- Member library, multi-field filters, three-state sorting, persistent column order/visibility,
  and configurable combined name/address components.
- Add/edit member with a required-field guard, button-only submission, title-assisted editable
  gender, postal-code/address assistance, automatic age, approval date, and coverage date.
- Up to two equal-share beneficiaries.
- Death entry, 365-day operator safeguard, live configurable service-fee calculation, advance return, immutable snapshots,
  certificate PDF storage/download, and death registry.
- Advance-unit reset counter and first-day/over-25-deaths notifications.
- Monthly member, death, and ส.ฌ.ก.1 PDF reports.
- Change history and database backup.

## Known MVP boundaries

- Authentication, installation locking, permissions, billing, receipts, and production migration are later work.
- Address assistance covers the demo operating area (อำเภอร้องกวาง จังหวัดแพร่); fields stay editable.
- The special non-payment disease list is not encoded. The operator chooses the checkbox and supplies
  the reason; the 365-day calculation is only a safeguard.
- Reset with zero members has no member balance to change; this known MVP condition is documented for manual testing.
- QuestPDF uses its evaluation license. Confirm a suitable production license before business deployment.

## Develop from source

Install the .NET SDK version in `global.json`, then run:

```powershell
dotnet restore ChapanakitCare.sln --locked-mode
dotnet build ChapanakitCare.sln --no-restore --configuration Release
dotnet test ChapanakitCare.sln --no-build --configuration Release
```

The previous implementation was archived at
`D:\Programing\Personal Projects\Chapanakit-Care-previous-version-2569-08-25` before this restart.
