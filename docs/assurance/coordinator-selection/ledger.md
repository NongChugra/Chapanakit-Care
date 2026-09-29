# Coordinator selection assurance ledger

- ASSURANCE_UNIT_ID: chapanakit-care/coordinators/initial-selection
- REOPEN_GENERATION: 0
- UNIT_STATUS: ship
- Execution: auto; parent owns backend, integration, and verification; a bounded Terra lane may own Razor UI.
- Assurance: final-strict (SQLite migration, role uniqueness, concurrency, and member lifecycle).
- Parent runtime observed: current task turn_context reports gpt-5.6-sol / xhigh.
- Base: master at a252d27364b80a58df4dd660ff97b1f24bba8df4; exact pre-existing dirty state and file hashes in base-state.json. Preserve those changes.
- Continuity: no earlier assurance unit or review attempts for this feature found.
- REVIEW_BUDGET_MODE: default; TARGET_REVIEW_CALLS: 1; MAX_REVIEW_CALLS: 3; REVIEW_CALLS_USED: 1. Authority: installed Solweaver default policy.
- ATTEMPT_COORDINATION_LOCATION: this directory/attempts.json, guarded by exclusive .NET FileMode.CreateNew of this directory/review.lock. Reservation must re-read identity, generation, budget, and passing readiness while holding that lock, persist the transition, then release. A busy lock cannot consume a call.
- Declared metadata exclusion: docs/assurance/coordinator-selection/** only. All changed product, tests, and design documentation belong to the behavior candidate. Unrelated pre-existing changes are identified against base-state.json.
- Final boundary: integrated coordinator selection, library, and dashboard in source; additive migration verified on isolated SQLite, lifecycle and HTTP/browser behavior verified. No installed application, user database, deployment, release, or real-data migration belongs to this boundary.

## Accepted product contract

1. Two organizational roles only: ประธาน and หัวหน้ากลุ่ม. Roles do not confer software permissions.
2. At most one current role per member, one current chairperson, and one current leader per group.
3. Only normal, nonarchived members can be selected. A leader must have exactly the selected GroupNo.
4. Explicit appoint, replace, and end actions; stale forms cannot overwrite newer assignments. Replacement and end require a reason. Dates are Gregorian DateOnly internally, Buddhist Era at display.
5. Preserve append-only change history with member name/number snapshots. Resignation/death end a role atomically with the status transition. Moving a current leader to another group requires ending the role first.
6. Coordinator page follows the approved visual direction; search/select member, group vacancies and incumbents, clear confirmation and validation, readable history.
7. Library has role column/filter and link to manage; dashboard shows current chairperson, group leaders, and vacant groups without replacing existing functionality.
8. Preserve report UI fixes and reference assets; report layout and new report filtering remain deferred.

## Architecture decision

Use existing Member.GroupNo as the exact group identity for this selection-only release. Add coordinator_positions (current projection plus optimistic version) and coordinator_events (immutable transition snapshots). Unique indexes enforce one slot and one role per member. Keep role codes fixed to the two accepted values. A separate village master and configurable role catalog would require group reconciliation and administration outside this release; do not invent either from ambiguous address text.

All application mutations use one SQLite transaction. Member update blocks moving an active leader. Death/resignation close the position and append the reason within their existing transaction. Persistence guards protect role history from ordinary tracked edits and prevent ineligible member/group mutations from leaving a current role behind. Demo reset explicitly clears the new dependent demo tables in its existing transaction.

## Verification plan

Focused: real SQLite service/invariant/lifecycle tests and real HTTP Razor form tests, observing meaningful RED before behavior code. Candidate: formatting on changed files, Release build and all tests, EF drift and additive upgrade, fresh isolated published web smoke, report PDF regression/visual inspection, and backup/restore evidence. Compose is not applicable to this local app. Perform parent adversarial readiness and machine packet validation before reserving the fresh final reviewer.

## Evidence

- Preflight: installed Solweaver + bundled TDD static validation passed. TDD and writing-good-tests guidance loaded before production edits.
- Selection/schema RED: backend-red.log, 18 failures after type-only seams; missing coordinator behavior and expected table names. An initial unread-constructor-parameter compile error was corrected before accepting RED.
- Selection GREEN and lifecycle RED: integrity-red.log, 19 passing and 10 expected failing tests. Failures demonstrated surviving roles on resignation/death, unguarded group/status/archive edits, editable history, missing rollback linkage, and demo-reset FK failure.
- Integrated backend GREEN: backend-green.log, 29/29 passing. Additional counterexample assertions were subsequently added for replacement rollback and death rollback; these are included in final candidate verification.
- Migration RED: migration-red.log, HasPendingModelChanges returned true. A test-analyzer error was corrected before accepting the actual assertion failure.
- Migration GREEN: migration-green.log, 9/9 schema, upgrade/restore and competing-request checks. The upgrade preserved member identity, number, balance and creation time; backup preserved positions, event snapshots and migration history.
- Scaffolding used the installed EF Core 10.0.10 design-time services (IMigrationsScaffolder), not hand-written SQL. The optional dotnet-ef install could not reach NuGet; a temporary console used already-cached packages via an explicit local package source. Migration output was moved into the existing Infrastructure/Persistence/Migrations folder. No application dependencies or lockfiles changed relative to the task baseline.
- Worker runtime inspected while active: persisted child 01a06a31-91dd-76d1-9597-324bd3d208f9 reports gpt-5.6-terra / max; final turn proof will be captured before accepting its report.
- Product design updated to the latest one-role / own-group / two-role decisions; stale prototype alternatives removed from the authoritative design document.

## Final candidate readiness

- FROZEN_CANDIDATE_ID: sha256:e9441d06460c6933b38217f8fa7eb57c1eff9ede7e8db5e10ee2ed1201d6f411
- ASSURANCE_PACKET_ID: coordinator-final-packet-v1:0e974574-8e38-4a63-a8fd-42d61202fad6; immutable call-1 packet/ledger/attempt snapshots bound by readiness SHA-256 records.
- TDD_REQUIRED: yes; complete focused evidence referenced above and in ui-tdd.md. No exceptions.
- All eight acceptance criteria: met. Complete cumulative parent inspection includes every path in candidate-manifest.json changedSinceTaskBase, with base-state.json preserving pre-existing dirty changes.
- Parent audit: parent-audit.md reconciled and PARENT_ADVERSARIAL_READY: yes. REVIEWABILITY: pass; one coherent selection workflow and invariant family fits a full review pass.
- Candidate checks: candidate-receipt.md plus script/logs. Latest full sequence passed 128 tests; build zero warnings/errors; exact final command inputs unchanged. Gate timestamps: restore/format 2026-09-04 06:17 UTC; build 06:18:02; tests 06:18:10; publish 06:18:12.
- Two frozen verification candidates: first exposed two pre-existing stale test assumptions, corrected in expected values only; second passed all checks. No production finance behavior changed. One full green pass on current candidate.
- Worker runtime: worker-runtime.json proves Terra/max. Worker stopped due usage limit without a final report. Parent owns all integrated UI implementation and evidence; no unverified worker completion claim was accepted.
- COMPOSE_CHECK and lifecycle: not applicable to this local Windows app. DELIVERY_ARTIFACT_MANIFEST and bundled TDD delivery: not applicable, source delivery only; this is not a skill/package installation or installed-app release.
- Protected boundaries crossed: none. User database, deployed app, real financial state, git commits/push/merge and external release remain untouched.
- Coordination proof: coordination-preflight.json records successful exclusive FileMode.CreateNew acquisition, read and release. reservation.ps1 repeats the same primitive and validates current bound hashes/identity/budget before reservation.
- Machine readiness: readiness.json, proof readiness-proof.json, exact command readiness-command.txt. Immutable pre-reservation ledger/attempt copies support replay after the authorized journal transitions.
- REVIEW_READY: yes; missing/not-run applicable evidence: none; known blockers: none; explicitly permitted nonblocking gaps: report formatting and new report selectors deferred by the user.

## Accepted final result

- WORK_STATUS: complete. ACCEPTANCE_STATUS: met. KNOWN_BLOCKERS: none.
- INDEPENDENT_ATTESTATION: obtained. FINAL_STATUS: ship. ASSURANCE_STATUS: final-strict-achieved.
- Fresh review: reviewer-verdict.md; complete pass, no findings. Actual reviewer runtime Sol/max, proven in reviewer-runtime.json from child 01a06b1f-3544-7540-9d8c-1d2e232962f9. Reviewer independently passed 44 coordinator tests.
- Calls reserved: 1; started/used: 1 of immutable default maximum 3; completed: 1 accepted ship; cancelled: 0. Same-attempt metadata closures: 0. Re-review rounds: 0. No mismatched/unverified runtime lane accepted.
- Exclusive completion: complete-call-1.json and attempts.json. Active reservation cleared, generation 0 closed as ship. Unused numeric budget cannot reopen this completed generation.
- Candidate unchanged at final acceptance: sha256:e9441d06460c6933b38217f8fa7eb57c1eff9ede7e8db5e10ee2ed1201d6f411. The pre-reservation proof is replayable with ledger-call-1.md and attempts-before-call-1.json; later journal/ledger mutations are intentional assurance accounting only.
- Testing classification clarification: core service/lifecycle/migration/HTTP production behavior followed RED/GREEN. Reversible table-rendering adjustments used direct browser evidence under the applicable developer instruction not to write tests for reversible low-impact changes. No historical RED is claimed for those adjustments; reviewer accepted that scope classification.
- Isolated published server stopped and temporary browser tab closed; viewport override reset. User data untouched. No commit, push, merge, production migration, install or release performed. Owner-accepted release authority: not applicable.
- User entry point: restart the normal development app with run-dev.bat, then ระบบจัดการและบันทึกข้อมูล → กำหนดผู้ประสานงาน. Normal startup applies the additive schema migration; no appointments are inferred.

## Post-phase retrospective

- Terminal result: ship for chapanakit-care/coordinators/initial-selection, generation 0.
- Candidate attempts: 2 frozen verification candidates; 1 candidate presented to final readiness and reviewer.
- Candidate gate executions: first candidate failed two stale legacy assertions; second candidate passed the full restore/format/build/test/publish sequence once. Successful full-suite evidence was not redundantly rerun. Reviewer ran only the focused 44-test coordinator lane.
- Focused executions are recorded in backend-red/green, integrity-red, migration-red/green, integrated-focused, legacy-expectation-fix logs and the UI worker rollout referenced by ui-tdd.md. Full pre-compaction invocation count was not independently reconstructed; no invented total is claimed.
- Full Compose executions: 0, not applicable. Candidate receipt reuse after freeze: only unchanged-candidate metadata accounting and final handoff, with freeze checks passing. Browser/PDF fixture outputs are verification evidence only.
- Reserved/started/completed review calls: 1/1/1. No cancelled calls, metadata closure or re-review. Start and completion timestamps are recorded in attempts.json; total task active time was not separately measured.

| Finding or friction | Class / origin | Resolution |
| --- | --- | --- |
| Old tests expected 15-baht settings despite baseline 9-baht seed | Evidence defect / pre-existing | Corrected expected values only; focused and full tests pass; production money logic unchanged |
| Solution formatter named pipe denied by sandbox | Harness limitation | Used changed-file whitespace checks with src/tests folder workspaces |
| UI worker reached usage limit | Runtime interruption | Parent inspected shared edits and completed integration/verification |
| Reviewer queried JavaScript TDD classification | Evidence clarification | Distinguished reversible rendered-cell order from persisted data changes and cited governing developer instruction; no source change or extra review call |

Generalizable workflow changes: none proposed in this delivery. WORKFLOW_CHANGE_STATUS: no-generalizable-change. No governance or skill files changed.
