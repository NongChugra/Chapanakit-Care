# Chapanakit Care

Local single-PC welfare-association application for the first-day scope defined on 25/08/69.

## Repository layout

```text
src/      Application, domain, EF Core migrations, and the synthetic fixture
tests/    Unit and real temporary-SQLite integration tests
docs/     Architecture, verification, GitHub handoff, and manual acceptance checklist
scripts/  One developer command for running, testing, publishing, and cleaning
tools/    Repeatable developer utilities
.github/  GitHub Actions CI
```

Generated packages, local SDK caches, runtime SQLite data, backups, and test/build output are
intentionally excluded from Git. See [GitHub handoff](docs/github-setup.md) before the first push.

The repository root includes one convenience launcher: double-click `run-dev.bat` to start the
development website with hot reload and open it at `http://127.0.0.1:5188`.

## Run a local published copy

Open `artifacts\publish\development\ChapanakitCare.Desktop.exe` and keep the complete `development` folder together.
It opens as a native Windows desktop window with the Chapanakit Care icon in the taskbar. The
development copy uses the installed .NET runtime, stores SQLite beside the executable in `App_Data`, and the
embedded interface listens only on `127.0.0.1:5188`; it does not open a browser window.

The launcher provides editable development constants, synthetic demo-member import, demo-data
clearing, and database backup. Demo import is safe when members already exist: new fixture members
are added and fixture members already present are skipped.

Important data behavior:

- Closing and reopening preserves the local database, table-column choices, order, and sorting.
- A confirmed death stores immutable member, beneficiary, settings, and money snapshots.
- A death certificate must be a real PDF of at most 10 MB and remains downloadable.
- **สำรองฐานข้อมูล** downloads a verified SQLite backup. Keep backups outside the app folder.
- **ล้างข้อมูลสมาชิกสาธิต** is destructive demo tooling, not production user management.

For a completely fresh demo, close the desktop application and delete only
`artifacts\publish\development\App_Data\chapanakit-care-demo.db`. The next launch creates an empty migrated
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
- รายงานสมาชิกทั้งหมด, รายงานสมาชิกประจำเดือน, รายงานแยกกลุ่ม และ ส.ฌ.ก.1 PDF reports.
- Change history and database backup.

## Member dates, recipient photos, and history

- Date controls display Buddhist Era (`19/07/2547`); requests and SQLite store Gregorian dates (`2004-07-19`). Invalid submissions preserve those dates. Existing stored dates are never guessed or rewritten automatically at startup.
- New applicants must be 20–60 completed years old on their application date. Existing historical/demo members remain editable; their admission age is not revalidated against this new rule.
- Each beneficiary has a **ใช้ที่อยู่เดียวกับสมาชิก** checkbox. While checked, its address follows the member's address; saving also enforces the copy on the server.
- Death entry accepts an optional JPG/PNG photo for each recipient, up to 10 MB per photo. Validated bytes are stored in SQLite in the same transaction as death confirmation and are included in backups. Download photos from the death registry. The reported certificate date is now persisted too.
- **ประวัติการเปลี่ยนแปลง** is available in the main navigation, with 50 entries per page and access to older history. Member/beneficiary edits, deaths, coordinator changes, settings, resignations, advance resets, and demo clearing are recorded. Searching, filtering, sorting, navigation, and table preferences do not add history. Demo clearing preserves existing audit entries.

JavaScript regression checks can be run with `node --test --test-isolation=none tests/js/*.test.mjs`, in addition to the .NET suite.

## Known MVP boundaries

- Authentication, installation locking, permissions, billing, receipts, and production migration are later work.
- Address assistance covers the demo operating area (อำเภอร้องกวาง จังหวัดแพร่); fields stay editable.
- The special non-payment disease list is not encoded. The operator chooses the checkbox and supplies
  the reason; the 365-day calculation is only a safeguard.
- Reset with zero members has no member balance to change; this known MVP condition is documented for manual testing.
- QuestPDF uses its evaluation license. Confirm a suitable production license before business deployment.

## Develop from source

Install the .NET SDK version in `global.json`. For normal development, double-click `run-dev.bat`
or use the consolidated developer command:

```powershell
.\scripts\dev.ps1 web
.\scripts\dev.ps1 desktop
.\scripts\dev.ps1 test
```

The `web` command uses ASP.NET Core hot reload and opens the browser. The `desktop` command starts
the WPF application shell. The `test` command restores locked dependencies, builds Release, and
runs the complete test suite.

The previous implementation was archived at
`D:\Programing\Personal Projects\Chapanakit-Care-previous-version-2569-08-25` before this restart.

## Publish the Windows desktop package

```powershell
.\scripts\dev.ps1 publish
```

The generated development application is written to `artifacts\publish\development`. Run
`.\scripts\dev.ps1 clean` to remove reproducible build, test, report, database, and publish output.

The WebView2 Runtime is included with supported Windows 10 and Windows 11 installations through
Microsoft Edge. If it has been removed from a PC, install the Microsoft Edge WebView2 Runtime
before opening the application.

## Demo

Follow the Thai end-to-end demonstration sequence in [docs/demo-script.md](docs/demo-script.md).
The included fixture is synthesized from the supplied workbook structure while replacing personal
identifiers and names with demo values; it is not a production data migration.
