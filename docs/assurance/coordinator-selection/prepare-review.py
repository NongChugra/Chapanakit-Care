"""Build immutable call-1 packet snapshots and canonical machine readiness input."""
from pathlib import Path
import hashlib
import json
import uuid

d = Path(__file__).resolve().parent
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
attempts = json.loads((d / "attempts.json").read_text(encoding="utf-8-sig"))
assert attempts["reviewCallsUsed"] == 0 and not attempts["activeReviewReservation"] and not attempts["attempts"]
manifest = json.loads((d / "candidate-manifest.json").read_text(encoding="utf-8"))
candidate = manifest["frozenCandidateId"]
attempt_id = str(uuid.uuid4())
packet_id = "coordinator-final-packet-v1:" + str(uuid.uuid4())
audit = d / "parent-audit.md"
audit.write_text(audit.read_text(encoding="utf-8") + "\n## Final reconciliation\n\nAll matrix rows pass against candidate-receipt.md and the frozen candidate. All eight ledger acceptance criteria are met within this source delivery boundary. Product and architecture contradictions: none. Unresolved assumptions: none. Fix-induced regression checks: full 128-test suite and published browser/report checks pass. Candidate scope reconciled: all 29 changed-since-task-base paths, including untracked source/tests/design and additive migration designer/snapshot; unrelated baseline files remain byte-identical. PARENT_ADVERSARIAL_READY: yes. Reviewability: pass, one role-selection invariant family with explicit lifecycle, backup, migration and shared-view interactions.\n", encoding="utf-8")
ledger = d / "ledger.md"
ledger.write_text(ledger.read_text(encoding="utf-8") + f"""
## Final candidate readiness

- FROZEN_CANDIDATE_ID: {candidate}
- ASSURANCE_PACKET_ID: {packet_id}; immutable call-1 packet/ledger/attempt snapshots bound by readiness SHA-256 records.
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
""", encoding="utf-8")
paths = "\n".join("- " + p for p in manifest["changedSinceTaskBase"])
packet = f"""# Final-strict review packet — call 1

ROLE: Fresh read-only solweaver_reviewer. Do not edit files, implement fixes, commit, push, or orchestrate agents. Read applicable AGENTS.md and Solweaver contracts. English report. The parent has completed implementation and verification. Inspect actual source and complete cumulative changes independently.

EXECUTION MODE: auto. ASSURANCE MODE: final-strict.
ASSURANCE_UNIT_ID: chapanakit-care/coordinators/initial-selection
REOPEN_GENERATION: 0. UNIT_STATUS: open. UNIT_CONTINUITY_CHECKED: yes; no prior review attempts.
LEDGER_LOCATION: docs/assurance/coordinator-selection/ledger.md (immutable reviewed snapshot ledger-call-1.md).
ATTEMPT_COORDINATION_LOCATION: docs/assurance/coordinator-selection/attempts.json guarded by .NET FileMode.CreateNew of review.lock in the same directory.
RESERVATION_EVIDENCE: reservation-call-1.json plus attempts.json records acquisition, identity/hash reread, reservation transition, release and child start. This packet precedes reservation; inspect the durable transition before reviewing.
REVIEW_BUDGET_MODE: default. Authority: installed Solweaver new-unit policy. TARGET_REVIEW_CALLS: 1. MAX_REVIEW_CALLS: 3. REVIEW_CALLS_USED before call: 0.
REVIEW_ATTEMPT_ID: {attempt_id}. THIS_CALL: 1. CALL_STATE at dispatch: reserved (then started in journal). Re-review preparation: not applicable for first call.
FROZEN_CANDIDATE_ID: {candidate}
ASSURANCE_PACKET_ID: {packet_id}

OBJECTIVE AND ACCEPTANCE
Implement approved กำหนดผู้ประสานงาน page, shared library and overview views. Only chairperson and group leader; at most one role per member; one chair, one leader per exact existing Member.GroupNo; eligible normal/nonarchived members only. Leader must belong to assigned group. Explicit appoint/replace/end, required reason for replace/end, stale form rejection, immutable history and member snapshots. Atomic role ending on resignation/death, active leader group-change guard. Separate role from membership status and software permissions. Follow orange/brown existing UI; keep report layout redesign deferred. Full accepted criteria and architecture in ledger-call-1.md and docs/design/coordinator-roles.md.

BASE AND SCOPE
master HEAD a252d27364b80a58df4dd660ff97b1f24bba8df4 with pre-existing dirty changes exactly recorded in base-state.json. Baseline report UI GET fix and high-quality reference crops, project setup/lockfile changes are preserved, not new implementation. Compare baseline hashes and git diff; untracked new files require direct reading. candidate-manifest.json binds all cached/uncached nonignored files plus baseline deletions, excludes only this declared assurance metadata directory. freeze_candidate.py --check verifies current bytes. Complete changed-since-task-base scope (29 files):
{paths}

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
"""
(d / "review-packet.md").write_text(packet, encoding="utf-8")
(d / "ledger-call-1.md").write_bytes(ledger.read_bytes())
(d / "attempts-before-call-1.json").write_bytes((d / "attempts.json").read_bytes())
readiness = dict(schemaVersion="solweaver-final-strict-readiness-v1", assuranceUnitId=attempts["assuranceUnitId"],
    reopenGeneration=0, unitStatus="open", reviewReady=True, reviewBudgetMode="default", targetReviewCalls=1,
    maxReviewCalls=3, reviewCallsUsed=0, validationPhase="pre-reservation", activeReviewReservation=None,
    frozenCandidateId=candidate, assurancePacketId=packet_id,
    ledgerSha256=digest(ledger), attemptsSha256=digest(d / "attempts.json"), packetSha256=digest(d / "review-packet.md"),
    candidateManifestSha256=digest(d / "candidate-manifest.json"), parentAdversarialReady=True,
    reviewability="pass", candidateScopeComplete=True, protectedBoundaryCrossed=False, missingEvidence=[])
write(d / "readiness.json", readiness)
write(d / "intended-call-1.json", {"reviewAttemptId":attempt_id, "assurancePacketId":packet_id, "frozenCandidateId":candidate})
print(json.dumps({"attemptId":attempt_id,"packetId":packet_id,"candidate":candidate}))
