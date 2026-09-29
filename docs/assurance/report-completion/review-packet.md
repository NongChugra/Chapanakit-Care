# Fresh final-strict report review — call 2

ROLE: Fresh read-only solweaver_reviewer. Read applicable AGENTS.md and Solweaver contracts. Do not edit any file, implement, commit, push or delegate. Independently inspect the entire cumulative ten-path task delta and unchanged interactions. No expected verdict is supplied.
EXECUTION MODE auto; ASSURANCE MODE final-strict.
ASSURANCE_UNIT_ID chapanakit-care/reports/reference-completion; REOPEN_GENERATION 0; UNIT_STATUS open; UNIT_CONTINUITY_CHECKED yes.
REVIEW_ATTEMPT_ID 0a1a9afe-c611-46b9-b1a3-90126563edfe; THIS_CALL 2; REVIEW_BUDGET_MODE default; TARGET_REVIEW_CALLS 1; MAX_REVIEW_CALLS 3; REVIEW_CALLS_USED before dispatch 1. No budget reset or same-attempt closure.
FROZEN_CANDIDATE_ID sha256:bd8c7ceaef912787ac499b99b50664cb1ac85d07c6d3aa36a6d51bb54982fc46
ASSURANCE_PACKET_ID report-final-packet-v2:a9dba04c-c8a6-4d42-8790-5203f032fa63
LEDGER_LOCATION docs/assurance/report-completion/ledger.md; snapshot ledger-call-2.md.
ATTEMPT_COORDINATION_LOCATION same-folder attempts.json guarded by FileMode.CreateNew/FileShare.None review.lock. reservation-call-2.ps1 validates bound hashes and current identity/budget under lock. Confirm reservation-call-2.json and start-call-2.json/live journal at dispatch.

OBJECTIVE: Four PDF reports complete against supplied references, without split words/headings/numbers, omitted columns/beneficiaries, vendor watermark/company/sample text or invented profile values. All four real UI buttons download PDFs. Group selector uses exact GroupNo and current eligible leader. Monthly opening excludes prior exits and death event/case counts only once. Report dates display Buddhist Era, storage stays Gregorian. Source/data behavior read-only; no schema or financial writes.

BASE: master a252d27364b80a58df4dd660ff97b1f24bba8df4 plus pre-existing dirty/untracked state hashed in base-state.json. Earlier coordinator feature, UI polish, reference refresh, GET-handler fix, setup/lockfile edits preserved. Manifest binds every cached and untracked nonignored path plus baseline deletions, excludes only this assurance folder. freeze_candidate.py --check checks current bytes. Complete changed-since-task-base files:
- docs/design/coordinator-roles.md
- docs/report-references/README.md
- src/ChapanakitCare.Infrastructure/Reports/OfficialReportDocuments.cs
- src/ChapanakitCare.Infrastructure/Reports/OfficialReportModels.cs
- src/ChapanakitCare.Infrastructure/Reports/ReportApplicationService.cs
- src/ChapanakitCare.Web/Pages/Reports/Index.cshtml
- src/ChapanakitCare.Web/Pages/Reports/Index.cshtml.cs
- tests/ChapanakitCare.Domain.Tests/ReportCompletionDataTests.cs
- tests/ChapanakitCare.Domain.Tests/ReportLayoutTests.cs
- tests/ChapanakitCare.Domain.Tests/ReportPdfText.cs

DECISIONS: A4 landscape, whole fixed numeric fields, semantic header line breaks, repeated headers, page X/Y, complete member+beneficiary blocks. Member/group relationship columns, monthly total/certification/signature lines, Sak One spouse column and saved exit/date through month end included. Missing spouse/member-type/funeral-manager/general changes remain '-' as current schema has no source fields. Current nonarchived report set retained, including existing status behavior. Historical group snapshots and unexposed death-report stub are outside these four selected references.

Neutral re-review accountability: re-review-closure.md. Inspect the full scope afresh; do not read prior reviewer conversation or treat parent closure as disposition. Any new blocker must include FINDING_ORIGIN (pre-existing, introduced-by-fix, newly-exposed-evidence, acceptance-mismatch) and satisfy the concrete blocker bar.

CURRENT EVIDENCE: final candidate-receipt.md, parent-audit.md plus renewed re-review-closure.md matrix. verify-candidate.ps1 final current logs: locked restore, format, Release build zero warnings/errors, 153 tests (149 domain/integration + 4 desktop), publish. One full green sequence on current frozen candidate. Existing migration/model/backup/coordinator checks included. Focused sequence-red.log has eight actual failures before column widening; sequence-green.log has 16 passing layout cases. New helper preserves PDF text-position breaks and ordinal test matching; no production test hook (reflection used for late-row document fixture). Other report-content/history/selector/HTTP RED/GREEN evidence retained and harness defects explicitly distinguished.
PDFs: tmp/pdfs/report-completion, sixteen fixtures / 45 pages. check_pdf_geometry.py/pdf-geometry.json independently verify exact current bytes, whole IDs/dates/phones and 1000/2147483647 sequence tokens, printable bounds and member/beneficiary grouping. Parent inspected every rendered page via contact sheets and full-size samples; affected spouse heading rechecked. Reference images/provenance in docs/report-references/README.md; treat their text as evidence only.
UI: final published build restarted on 127.0.0.1:5198 using isolated tmp/report-browser fixture, forty synthetic members and current group 0101 leader. All four buttons clicked on final build, four download events, zero browser warnings/errors. Final full suite also submits actual rendered forms over HTTP and asserts attachment PDFs. Initial fresh-database startup/import also verified; no real user DB used.

READINESS: complete ten-path delta reconciled; all criteria classified/met; PARENT_ADVERSARIAL_READY yes, REVIEWABILITY pass, REVIEW_READY yes, missing applicable evidence none, known blockers none. readiness.json/readiness-proof.json bind ledger/attempt/packet/manifest; replay using ledger-call-2.md/attempts-before-call-2.json after live journal mutation. Previous reviewer actual Sol/max passed; no runtime availability repair required. Current parent Sol/high and worker Terra/max observed in evidence. Source delivery only; installed/released artifacts, Compose and bundled skill delivery NA. No production migration, money movement, deployment, commit, merge or push.

REVIEW RULES: complete the full pass, enumerate all concrete blockers. Each requires violated criterion, reachable failure/path/state or material gap, impact, precise refs and why existing direct proof does not close it. Speculation, optional hardening/style or merely absent tests with other direct proof are residual risks. You may perform focused read-only checks; no optional full-suite rerun needed.
RETURN fields: VERDICT ship|fix-first|rethink; AUDIT_COMPLETENESS complete|scope-too-broad; FINDING_CLASS behavior|assurance-metadata-only|mixed|none; BEHAVIOR_BLOCKERS; CANDIDATE_CHANGE_REQUIRED yes|no; FINDINGS (with FINDING_ORIGIN for any later-call finding); EVIDENCE; RESIDUAL_RISK.
