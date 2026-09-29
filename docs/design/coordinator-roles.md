# กำหนดผู้ประสานงาน — approved first release

Updated 2026-09-04 (04/09/2569). The user approved the orange/brown visual design
and requested implementation, with **one role per member**, **leaders from their
own group**, and **only ประธาน / หัวหน้ากลุ่ม**. Those decisions supersede the
earlier prototype's multiple-role and cross-group examples.

## Purpose and decisions

An operator selects an existing member for an organizational responsibility.
The same current appointment appears in กำหนดผู้ประสานงาน, ทะเบียนสมาชิก and
ภาพรวม. These positions confer no software permissions. The Obsidian restart
plan remains the broader product authority; login and accounting remain deferred.

| Rule | First-release decision | Reason |
| --- | --- | --- |
| ประธาน | One current chairperson for the association | Gives one unambiguous contact. |
| หัวหน้ากลุ่ม | One current leader per existing GroupNo | Shows exactly which group has a leader or vacancy. |
| Member capacity | At most one current role, enforced by a unique database index | A member cannot simultaneously be chairperson and leader or lead several groups. |
| Eligibility | Normal, nonarchived member; leader's GroupNo must exactly match the position | Enforces the user's own-group rule in the picker and on save. |
| Effective date | Today, stored as Gregorian DateOnly and displayed in Buddhist Era | Avoids scheduling and backdated overlap rules. |
| Replacement / ending | Explicit action with a required reason | Prevents an ordinary appointment from silently replacing someone. |
| Other roles | No role creation or additional role choices | Capacity and eligibility must be decided before adding another role. |

Use current positions plus append-only events instead of a `members.role` text
column. The extra table gives vacancies, replacement history and stale-form
protection a clear home. The visible role column is derived, so the three pages
cannot drift into separate role records.

Use the existing **GroupNo** as group identity. It is already the membership
group used by this app. Do not infer identity from village names or moo numbers,
which may repeat. A normalized village directory could be added later if the
association needs to reconcile group codes with administrative villages. This
release neither merges codes nor moves members during appointment.

## Coordinator page

Route `/Coordinators`. Enable the existing navigation entry. Keep the approved
ivory background, white panels, brown navigation and orange actions.

- Current positions show the chairperson first, then group leaders, including
  vacancies. Each row shows role, exact group, member name/number and appointment
  date. Occupied rows offer replace/end; vacant rows offer appoint.
- Selecting a row opens a form beside the roster on wide screens and below it
  on narrow screens. Search by member number or name, then explicitly select a
  result. The group-leader picker contains only eligible members from that group.
- Display the selected responsibility and incumbent in the form. A member
  holding any role is unavailable until their previous role ends.
- Replacement and ending require a reason. A failed validation keeps the form
  available; a stale form requires reloading and reviewing the latest selection.
- Success uses redirect-after-post. Retrying an old form cannot create another
  event or overwrite a newer holder because its version is no longer current.
- History shows the latest 100 transitions with before/after member snapshots,
  role/group, date, reason and actor. There is no edit/delete history control.

Use native labeled controls, visible keyboard focus, readable Thai validation
and wrapping actions. Search is a GET; mutations are antiforgery-protected POSTs.
No national ID or birth date is needed in the appointment picker.

## Member library and overview

ทะเบียนสมาชิก gains a **ตำแหน่ง** column near the name, with ประธาน,
หัวหน้ากลุ่ม plus group, or a dash. Keep membership **สถานะ** separately.
Add a role filter (all / chairperson / group leader / no role) that composes
with existing filters, and a link to manage appointments. Existing column
visibility/order preferences remain usable and reset includes the new column.

ภาพรวม shows the current chairperson, leader rows and vacant group count,
with navigation to coordinator selection. Existing membership totals and demo
controls remain available. The summary reads the same current-position projection.

## Database and service boundary

`coordinator_positions` is the mutable current projection:

| Field | Purpose |
| --- | --- |
| PositionKey | Primary key: `chairperson` or `group:` + exact group code. |
| RoleCode / GroupNo | Two fixed codes; chairperson has no group, leader requires a nonblank group. A check constraint binds the key to its scope. |
| MemberId | Nullable FK to members; filtered unique index allows at most one current role per member. Null means vacant. |
| AppointedOn | Gregorian DateOnly while occupied; null while vacant. |
| Version | Incremented on every transition and checked for optimistic concurrency. Keep the row after ending so old vacant forms remain stale. |

`coordinator_events` stores immutable transitions, unique per position/version:
action, old/new member IDs and name/number snapshots, role/group, effective date,
UTC timestamp, actor and reason. History remains readable after a member changes
their name. Each transition also writes a generic audit event.

`CoordinatorApplicationService` owns current-role reads, eligible member search,
and appointment/replacement/ending. A single SQLite transaction revalidates the
posted key, action, member, version, incumbent, eligibility and group; updates the
projection; appends history/audit; and commits. Database uniqueness and check
constraints back up application validation. GET requests never create rows.

Death confirmation and resignation call the same transition helper inside their
existing transactions. The role ends with a reason and business date together
with the member status and ledger changes. Failure rolls back all of them.
Moving a current leader to another group is blocked until their role is ended.
The persistence guard also rejects tracked status/archive/group changes that
would leave an ineligible holder, and rejects edits/deletes of recorded role
events. The existing explicit demo reset clears these demo tables in dependency
order inside its transaction. SQLite backup includes both tables automatically.

The migration is additive: two tables plus indexes/constraints, with no member
rewrite, inferred appointments or production workbook migration. Verify upgrade
from the previous schema and restore from a backup with real temporary SQLite.

## Deferred reports

The refreshed [four reference images](../report-references/README.md) remain the
report layout evidence. The original first-page image was separated into
all-members and members-under-leader sections. The later report-completion task
now implements their formatting and current group/leader selector.

The leader report resolves the current selected group's leader position and
filters members by that exact GroupNo. Selecting a chairperson alone
does not identify a group. Historical reports would additionally need membership
group history; appointment events alone cannot reconstruct past group rosters.

## Acceptance and verification

1. Assign a chairperson or own-group leader and show the same holder on all three pages.
2. Reject a second role, wrong-group member, ineligible member, unknown role and silent replacement.
3. Require reason and explicit action for replacement/end; retain immutable before/after snapshots.
4. Reject stale and competing forms; ending and reappointing must retain monotonically increasing versions.
5. End roles atomically during death/resignation; roll back on injected write failure.
6. Block moving a current leader; permit it after ending their role.
7. Preserve existing members on schema upgrade and preserve roles/history on backup/restore.
8. Verify real HTTP forms, browser selection/confirmation, library filters, dashboard and narrow layout.

Detailed RED/GREEN, candidate and independent review evidence is tracked in
[the coordinator assurance ledger](../assurance/coordinator-selection/ledger.md).
