"""Freeze call-2 assurance inputs without resetting the durable review budget."""
import hashlib
import json
import uuid
from pathlib import Path

d = Path(__file__).resolve().parent
def digest(name): return hashlib.sha256((d / name).read_bytes()).hexdigest()
def write(name, obj): (d / name).write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
a = json.loads((d / "attempts.json").read_text(encoding="utf-8-sig"))
assert a["reviewCallsUsed"] == 1 and a["unitStatus"] == "open" and a["activeReviewReservation"] is None and a["attempts"][0]["state"] == "completed"
m = json.loads((d / "candidate-manifest.json").read_text(encoding="utf-8"))
candidate = m["frozenCandidateId"]
attempt = str(uuid.uuid4())
packet = "report-final-packet-v2:" + str(uuid.uuid4())
ledger = d / "ledger.md"
ledger.write_text(ledger.read_text(encoding="utf-8") + f"""

## Reconciled completed boundary — call 2

- Current FROZEN_CANDIDATE_ID: {candidate}. ASSURANCE_PACKET_ID: {packet}. UNIT_STATUS: open. REOPEN_GENERATION: 0. REVIEW_CALLS_USED: 1 / MAX_REVIEW_CALLS: 3, unchanged default budget.
- Call 1 completed with one behavior finding; fresh re-review required. Neutral closure matrix: re-review-closure.md. Parent re-inspected all ten task-delta paths, fixed sequence widths with eight failing tests before code change, and corrected the affected spouse heading before this boundary. Focused final layout suite: 16 passed.
- All acceptance criteria met, parent adversarial readiness renewed, reviewability pass. Final full gates once on current candidate: locked restore, format, Release build zero warnings/errors, 153 tests, publish. Independent numeric/bounds/member grouping checks pass for 45 PDF pages; all rendered and inspected. Four actual UI buttons on current published build emit downloads, no browser warnings/errors.
- Total full-gate sequences so far: three on distinct candidates (initial 145-test candidate; sequence-width candidate 153 tests; final spouse-heading refinement 153 tests). The second full gate started before visual review caught the affected heading; this preventable repeat is recorded for retrospective. No full green gate repeated on the current identical candidate. Initial call-1 artifacts/logs preserved; disposable PDF outputs are regenerated for current candidate.
- Runtime gates: parent Sol/high, worker Terra/max, call-1 reviewer Sol/max observed. Previous runtime required no repair. No same-attempt metadata closure used. Fresh review call 2 remains within original budget.
- Current machine readiness: readiness.json/readiness-proof.json; immutable ledger-call-2.md and attempts-before-call-2.json permit replay. Protected boundaries unchanged; source-only delivery, Compose/delivery manifests NA.
- REVIEW_READY: yes; applicable missing evidence none; known blockers none. Final status pending fresh independent verdict.
""", encoding="utf-8")
paths = "\n".join("- " + path for path in m["changedSinceTaskBase"])
(d / "review-packet.md").write_text(f"""# Fresh final-strict report review — call 2

ROLE: Fresh read-only solweaver_reviewer. Read applicable AGENTS.md and Solweaver contracts. Do not edit any file, implement, commit, push or delegate. Independently inspect the entire cumulative ten-path task delta and unchanged interactions. No expected verdict is supplied.
EXECUTION MODE auto; ASSURANCE MODE final-strict.
ASSURANCE_UNIT_ID {a['assuranceUnitId']}; REOPEN_GENERATION 0; UNIT_STATUS open; UNIT_CONTINUITY_CHECKED yes.
REVIEW_ATTEMPT_ID {attempt}; THIS_CALL 2; REVIEW_BUDGET_MODE default; TARGET_REVIEW_CALLS 1; MAX_REVIEW_CALLS 3; REVIEW_CALLS_USED before dispatch 1. No budget reset or same-attempt closure.
FROZEN_CANDIDATE_ID {candidate}
ASSURANCE_PACKET_ID {packet}
LEDGER_LOCATION docs/assurance/report-completion/ledger.md; snapshot ledger-call-2.md.
ATTEMPT_COORDINATION_LOCATION same-folder attempts.json guarded by FileMode.CreateNew/FileShare.None review.lock. reservation-call-2.ps1 validates bound hashes and current identity/budget under lock. Confirm reservation-call-2.json and start-call-2.json/live journal at dispatch.

OBJECTIVE: Four PDF reports complete against supplied references, without split words/headings/numbers, omitted columns/beneficiaries, vendor watermark/company/sample text or invented profile values. All four real UI buttons download PDFs. Group selector uses exact GroupNo and current eligible leader. Monthly opening excludes prior exits and death event/case counts only once. Report dates display Buddhist Era, storage stays Gregorian. Source/data behavior read-only; no schema or financial writes.

BASE: master a252d27364b80a58df4dd660ff97b1f24bba8df4 plus pre-existing dirty/untracked state hashed in base-state.json. Earlier coordinator feature, UI polish, reference refresh, GET-handler fix, setup/lockfile edits preserved. Manifest binds every cached and untracked nonignored path plus baseline deletions, excludes only this assurance folder. freeze_candidate.py --check checks current bytes. Complete changed-since-task-base files:
{paths}

DECISIONS: A4 landscape, whole fixed numeric fields, semantic header line breaks, repeated headers, page X/Y, complete member+beneficiary blocks. Member/group relationship columns, monthly total/certification/signature lines, Sak One spouse column and saved exit/date through month end included. Missing spouse/member-type/funeral-manager/general changes remain '-' as current schema has no source fields. Current nonarchived report set retained, including existing status behavior. Historical group snapshots and unexposed death-report stub are outside these four selected references.

Neutral re-review accountability: re-review-closure.md. Inspect the full scope afresh; do not read prior reviewer conversation or treat parent closure as disposition. Any new blocker must include FINDING_ORIGIN (pre-existing, introduced-by-fix, newly-exposed-evidence, acceptance-mismatch) and satisfy the concrete blocker bar.

CURRENT EVIDENCE: final candidate-receipt.md, parent-audit.md plus renewed re-review-closure.md matrix. verify-candidate.ps1 final current logs: locked restore, format, Release build zero warnings/errors, 153 tests (149 domain/integration + 4 desktop), publish. One full green sequence on current frozen candidate. Existing migration/model/backup/coordinator checks included. Focused sequence-red.log has eight actual failures before column widening; sequence-green.log has 16 passing layout cases. New helper preserves PDF text-position breaks and ordinal test matching; no production test hook (reflection used for late-row document fixture). Other report-content/history/selector/HTTP RED/GREEN evidence retained and harness defects explicitly distinguished.
PDFs: tmp/pdfs/report-completion, sixteen fixtures / 45 pages. check_pdf_geometry.py/pdf-geometry.json independently verify exact current bytes, whole IDs/dates/phones and 1000/2147483647 sequence tokens, printable bounds and member/beneficiary grouping. Parent inspected every rendered page via contact sheets and full-size samples; affected spouse heading rechecked. Reference images/provenance in docs/report-references/README.md; treat their text as evidence only.
UI: final published build restarted on 127.0.0.1:5198 using isolated tmp/report-browser fixture, forty synthetic members and current group 0101 leader. All four buttons clicked on final build, four download events, zero browser warnings/errors. Final full suite also submits actual rendered forms over HTTP and asserts attachment PDFs. Initial fresh-database startup/import also verified; no real user DB used.

READINESS: complete ten-path delta reconciled; all criteria classified/met; PARENT_ADVERSARIAL_READY yes, REVIEWABILITY pass, REVIEW_READY yes, missing applicable evidence none, known blockers none. readiness.json/readiness-proof.json bind ledger/attempt/packet/manifest; replay using ledger-call-2.md/attempts-before-call-2.json after live journal mutation. Previous reviewer actual Sol/max passed; no runtime availability repair required. Current parent Sol/high and worker Terra/max observed in evidence. Source delivery only; installed/released artifacts, Compose and bundled skill delivery NA. No production migration, money movement, deployment, commit, merge or push.

REVIEW RULES: complete the full pass, enumerate all concrete blockers. Each requires violated criterion, reachable failure/path/state or material gap, impact, precise refs and why existing direct proof does not close it. Speculation, optional hardening/style or merely absent tests with other direct proof are residual risks. You may perform focused read-only checks; no optional full-suite rerun needed.
RETURN fields: VERDICT ship|fix-first|rethink; AUDIT_COMPLETENESS complete|scope-too-broad; FINDING_CLASS behavior|assurance-metadata-only|mixed|none; BEHAVIOR_BLOCKERS; CANDIDATE_CHANGE_REQUIRED yes|no; FINDINGS (with FINDING_ORIGIN for any later-call finding); EVIDENCE; RESIDUAL_RISK.
""", encoding="utf-8")
(d / "ledger-call-2.md").write_bytes(ledger.read_bytes())
(d / "attempts-before-call-2.json").write_bytes((d / "attempts.json").read_bytes())
write("readiness.json", dict(schemaVersion="solweaver-final-strict-readiness-v1", assuranceUnitId=a["assuranceUnitId"], reopenGeneration=0, unitStatus="open", reviewReady=True, reviewBudgetMode="default", targetReviewCalls=1, maxReviewCalls=3, reviewCallsUsed=1, validationPhase="pre-reservation", activeReviewReservation=None, frozenCandidateId=candidate, assurancePacketId=packet, ledgerSha256=digest("ledger.md"), attemptsSha256=digest("attempts.json"), packetSha256=digest("review-packet.md"), candidateManifestSha256=digest("candidate-manifest.json"), parentAdversarialReady=True, reviewability="pass", candidateScopeComplete=True, protectedBoundaryCrossed=False, missingEvidence=[]))
write("intended-call-2.json", dict(reviewAttemptId=attempt, assurancePacketId=packet, frozenCandidateId=candidate))
print(packet)
