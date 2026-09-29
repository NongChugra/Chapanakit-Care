# Final-strict review packet — call 1

ROLE: Fresh read-only solweaver_reviewer. Do not edit files, implement fixes, commit, push, or orchestrate agents. Read applicable AGENTS.md and Solweaver contracts. English report. The parent has completed implementation and verification. Inspect actual source and complete cumulative changes independently.

EXECUTION MODE: auto. ASSURANCE MODE: final-strict.
ASSURANCE_UNIT_ID: chapanakit-care/coordinators/initial-selection
REOPEN_GENERATION: 0. UNIT_STATUS: open. UNIT_CONTINUITY_CHECKED: yes; no prior review attempts.
LEDGER_LOCATION: docs/assurance/coordinator-selection/ledger.md (immutable reviewed snapshot ledger-call-1.md).
ATTEMPT_COORDINATION_LOCATION: docs/assurance/coordinator-selection/attempts.json guarded by .NET FileMode.CreateNew of review.lock in the same directory.
RESERVATION_EVIDENCE: reservation-call-1.json plus attempts.json records acquisition, identity/hash reread, reservation transition, release and child start. This packet precedes reservation; inspect the durable transition before reviewing.
REVIEW_BUDGET_MODE: default. Authority: installed Solweaver new-unit policy. TARGET_REVIEW_CALLS: 1. MAX_REVIEW_CALLS: 3. REVIEW_CALLS_USED before call: 0.
REVIEW_ATTEMPT_ID: 923cca26-6309-4f75-8f5d-da107da3d24e. THIS_CALL: 1. CALL_STATE at dispatch: reserved (then started in journal). Re-review preparation: not applicable for first call.
FROZEN_CANDIDATE_ID: sha256:e9441d06460c6933b38217f8fa7eb57c1eff9ede7e8db5e10ee2ed1201d6f411
ASSURANCE_PACKET_ID: coordinator-final-packet-v1:0e974574-8e38-4a63-a8fd-42d61202fad6

OBJECTIVE AND ACCEPTANCE
Implement approved กำหนดผู้ประสานงาน page, shared library and overview views. Only chairperson and group leader; at most one role per member; one chair, one leader per exact existing Member.GroupNo; eligible normal/nonarchived members only. Leader must belong to assigned group. Explicit appoint/replace/end, required reason for replace/end, stale form rejection, immutable history and member snapshots. Atomic role ending on resignation/death, active leader group-change guard. Separate role from membership status and software permissions. Follow orange/brown existing UI; keep report layout redesign deferred. Full accepted criteria and architecture in ledger-call-1.md and docs/design/coordinator-roles.md.

BASE AND SCOPE
master HEAD a252d27364b80a58df4dd660ff97b1f24bba8df4 with pre-existing dirty changes exactly recorded in base-state.json. Baseline report UI GET fix and high-quality reference crops, project setup/lockfile changes are preserved, not new implementation. Compare baseline hashes and git diff; untracked new files require direct reading. candidate-manifest.json binds all cached/uncached nonignored files plus baseline deletions, excludes only this declared assurance metadata directory. freeze_candidate.py --check verifies current bytes. Complete changed-since-task-base scope (29 files):
- docs/design/coordinator-roles.md
- docs/report-references/README.md
- src/ChapanakitCare.Domain/Entities/Coordinators.cs
- src/ChapanakitCare.Infrastructure/Coordinators/CoordinatorApplicationService.cs
- src/ChapanakitCare.Infrastructure/Deaths/DeathApplicationServices.cs
- src/ChapanakitCare.Infrastructure/Members/MemberApplicationServices.cs
- src/ChapanakitCare.Infrastructure/Members/ResignationApplicationService.cs
- src/ChapanakitCare.Infrastructure/Persistence/AppDbContext.cs
- src/ChapanakitCare.Infrastructure/Persistence/CoordinatorWriteGuard.cs
- src/ChapanakitCare.Infrastructure/Persistence/Migrations/20260904022726_CoordinatorSelection.Designer.cs
- src/ChapanakitCare.Infrastructure/Persistence/Migrations/20260904022726_CoordinatorSelection.cs
- src/ChapanakitCare.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs
- src/ChapanakitCare.Web/Pages/Coordinators/Index.cshtml
- src/ChapanakitCare.Web/Pages/Coordinators/Index.cshtml.cs
- src/ChapanakitCare.Web/Pages/Index.cshtml
- src/ChapanakitCare.Web/Pages/Index.cshtml.cs
- src/ChapanakitCare.Web/Pages/Members/Index.cshtml
- src/ChapanakitCare.Web/Pages/Members/Index.cshtml.cs
- src/ChapanakitCare.Web/Pages/Shared/_Layout.cshtml
- src/ChapanakitCare.Web/Program.cs
- src/ChapanakitCare.Web/wwwroot/css/site.css
- src/ChapanakitCare.Web/wwwroot/js/member-table.js
- tests/ChapanakitCare.Domain.Tests/CheckpointFourWorkflowTests.cs
- tests/ChapanakitCare.Domain.Tests/CheckpointThreeWorkflowTests.cs
- tests/ChapanakitCare.Domain.Tests/CoordinatorIntegrityTests.cs
- tests/ChapanakitCare.Domain.Tests/CoordinatorMigrationTests.cs
- tests/ChapanakitCare.Domain.Tests/CoordinatorPageTests.cs
- tests/ChapanakitCare.Domain.Tests/CoordinatorWorkflowTests.cs
- tests/ChapanakitCare.Domain.Tests/DatabaseContractTests.cs

No installed/delivered runtime artifacts belong to this source delivery boundary. The isolated temporary published web copy and PDF renders are verification fixtures, not released binaries. Generated migration and model snapshot ARE included in candidate scope. No product/test/contract file excluded from behavior identity.

RISKS AND EVIDENCE
Data integrity: unique current member/slot, SQLite concurrency, immutable events, atomic status/role/financial rollback interactions, migration upgrade/backup. Inspect changed-to-unchanged lifecycle paths and form/JS preference integration. Parent matrix: parent-audit.md; complete acceptance classification and no assumptions unresolved.
Parent inspected actual backend and worker UI diff, tested live rendered controls, and passed candidate gates. candidate-receipt.md contains exact environment, result counts and observations. verify-candidate.ps1 is exact command recipe: locked restore, changed-file whitespace, Release build, all 128 .NET tests, isolated publish. Logs candidate-*.log prove latest full green sequence. EF model/upgrade/backup/race tests included. 40 synthetic-member browser fixture tested appoint/search/exclusion/replace/end/reason guard, library filter/manage/preferences, dashboard, desktop/narrow layout and no console errors. Three PDFs returned and all rendered pages inspected. Compose not applicable, no Compose app. No successful full gate repeated on this candidate.
First verification candidate exposed two legacy tests expecting 15-baht settings while baseline already seeds 9 baht with fee rounded down to whole baht. Only those expected values/comments changed; production calculation/settings unchanged. See candidate-1-tests-failed.log, legacy-expectation-fix.log and final full suite.
TDD_REQUIRED: yes. RED/GREEN per slice in backend-red.log, integrity-red.log, migration-red.log and corresponding green logs; UI evidence in ui-tdd.md. Worker observed Terra/max but stopped due usage limit, parent took ownership and verified complete source; worker-runtime.json. Parent observed Sol/xhigh in parent-runtime.json.

READINESS
REVIEW_READY: yes. REVIEWABILITY: pass. PARENT_ADVERSARIAL_READY: yes. Applicable missing evidence: none. Known blockers: none. Protected boundary crossed: false. Only source delivery and isolated test fixtures; no user DB migration, deploy/merge/release or money movement.
FINAL_STRICT_READINESS_RECORD_LOCATION: readiness.json. MACHINE_READINESS_PROOF: readiness-proof.json; exact validator command readiness-command.txt. Replay using ledger-call-1.md / attempts-before-call-1.json after intentional live journal mutations. Candidate/packet identities distinct, all input hashes bound. Re-read actual live reservation before accepting packet provenance.

REVIEW INSTRUCTIONS
Perform one complete pass even after finding a blocker; report ALL blocking issues. Blockers require the precise violated criterion, reachable failing path/state/evidence gap, user/data/security impact, precise file refs, and why current tests/direct proof do not close it. Speculative future hardening/style are residual risks, not blockers. Missing tests block only required/critical behavior without other direct proof. If too broad for full pass, return rethink with scope-too-broad. Parent self-review does not satisfy your gate. You may run focused read-only tests/inspection; do not change candidate or user data.

RETURN EXACT FIELDS
VERDICT: ship | fix-first | rethink
AUDIT_COMPLETENESS: complete | scope-too-broad
FINDING_CLASS: behavior | assurance-metadata-only | mixed | none
BEHAVIOR_BLOCKERS: list or none
CANDIDATE_CHANGE_REQUIRED: yes | no
FINDINGS: all evidence-qualified blockers or none
EVIDENCE: concrete supporting inspection/checks
RESIDUAL RISK: nonblocking limits or none
