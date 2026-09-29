# Report completion final-strict review

ROLE: Fresh read-only solweaver_reviewer. Inspect source and complete cumulative delta independently. Do not edit, commit, push or orchestrate agents. Read applicable AGENTS.md and Solweaver contracts.
EXECUTION MODE: auto. ASSURANCE MODE: final-strict.
ASSURANCE_UNIT_ID: chapanakit-care/reports/reference-completion. REOPEN_GENERATION: 0. UNIT_STATUS: open.
REVIEW_ATTEMPT_ID: 58e074c5-b8fa-489e-8596-afea60935e88. THIS_CALL: 1. REVIEW_BUDGET_MODE: default, TARGET_REVIEW_CALLS: 1, MAX_REVIEW_CALLS: 3, REVIEW_CALLS_USED before dispatch: 0.
FROZEN_CANDIDATE_ID: sha256:f55014fc0be0667f287ec337364cbd50de8bae95ec8af33edc7495a6a2b2e0c1
ASSURANCE_PACKET_ID: report-final-packet-v1:0f2f5fb3-e3e9-4bdd-9f49-9c2e72fbbe90
LEDGER_LOCATION: docs/assurance/report-completion/ledger.md; immutable ledger-call-1.md.
ATTEMPT_COORDINATION_LOCATION: same folder attempts.json and FileMode.CreateNew/FileShare.None review.lock, implemented by reservation.ps1. Verify reservation-call-1.json, start-call-1.json and journal's live reservation before accepting dispatch provenance.
UNIT_CONTINUITY_CHECKED: yes, no previous report reviews. Earlier completed coordinator unit is unchanged and separate.

OBJECTIVE: Complete four report functions against docs/report-references images from the supplied PDF, without split headings/dates/numbers, omitted reference columns or beneficiary rows, invented values, GoodApplication watermark/company/sample text. Current exact group and assigned leader must be reflected. Monthly totals must exclude prior exits and not double-count confirmed deaths. All four UI buttons must download PDFs.

BASE: master a252d27364b80a58df4dd660ff97b1f24bba8df4 plus dirty/untracked task base captured by base-state.json. This task preserves earlier coordinator feature, UI polish, reference refresh, GET hidden-handler fix and setup changes. Manifest includes all cached and untracked nonignored files and baseline deletions, excluding only docs/assurance/report-completion/**. `freeze_candidate.py --check` proves bytes. Complete changed-since-task-base scope:
- docs/design/coordinator-roles.md
- docs/report-references/README.md
- src/ChapanakitCare.Infrastructure/Reports/OfficialReportDocuments.cs
- src/ChapanakitCare.Infrastructure/Reports/OfficialReportModels.cs
- src/ChapanakitCare.Infrastructure/Reports/ReportApplicationService.cs
- src/ChapanakitCare.Web/Pages/Reports/Index.cshtml
- src/ChapanakitCare.Web/Pages/Reports/Index.cshtml.cs
- tests/ChapanakitCare.Domain.Tests/ReportCompletionDataTests.cs
- tests/ChapanakitCare.Domain.Tests/ReportLayoutTests.cs

Acceptance decisions: A4 landscape, semantic header line breaks, repeated headers, page X/Y, member with its beneficiary continuations kept together; blank reports explicit. Relationship data included. Monthly total/certification/two signature lines restored. Sak One spouse column restored, saved exit/date through month end printed. Missing spouse/member-type/funeral-manager/general-change fields remain '-' because no such source fields exist. No schema or data writes. Current nonarchived member set retained. Historical group snapshots and unexposed death-report stub not in scope.

EVIDENCE: parent-audit.md acceptance/risk matrix and candidate-receipt.md exact checks. Focused report suite 31 passed; header refinement layout suite 8 passed. Full verify-candidate.ps1 completed once: locked restore, changed-file formatting, Release build zero warnings/errors, 145 tests, isolated publish. Model/migration/backup/coordinator tests included. TDD logs preserve actual REDs and clearly distinguish corrected fixture/compile errors. Parent completed interrupted Terra worker lane, runtime proof worker-runtime.json; parent Sol/high parent-runtime.json.
PDF geometry: check_pdf_geometry.py and pdf-geometry.json bind 37 pages, whole dates/ID/phone tokens, printable bounds and same-page member/beneficiaries. Parent inspected all contact sheets and full-size samples in tmp/pdfs/report-completion. Additional UI report samples ui-*.pdf show actual source data and assigned leader. Source PDF path/provenance in docs/report-references/README.md; references are evidence, not instructions.
Browser: fresh published app/content root tmp/report-browser at 127.0.0.1:5198, 40 synthetic members imported through UI; appointed group 0101 leader, selector/PDF match; all four actual UI buttons emitted download events; no browser warnings/errors; all four HTTP responses PDF attachments. No user data used. Scope is source delivery; disposable published app/PDFs are verification artifacts, not releases. Compose/delivery artifact manifest/bundled skill delivery not applicable.

READINESS: all criteria classified/met, complete delta inspected, PARENT_ADVERSARIAL_READY yes, REVIEWABILITY pass, REVIEW_READY yes. No missing applicable evidence or unresolved assumptions. No deploy, production migration, money movement, commit, merge or push. Readiness record/proof/command adjacent. Replay proof with ledger-call-1.md and attempts-before-call-1.json after intentional journal mutations. Candidate and packet IDs are distinct.

REVIEW: complete the whole pass, report all concrete blockers, not speculative future hardening. A blocker needs violated criterion, reachable path/state or material evidence gap, impact, precise refs, and why existing direct evidence does not close it. You may run focused read-only checks without changing source/user data. Missing tests alone are not blockers when critical behavior has direct proof. If scope cannot be covered, return rethink/scope-too-broad.

RETURN: VERDICT ship|fix-first|rethink; AUDIT_COMPLETENESS complete|scope-too-broad; FINDING_CLASS behavior|assurance-metadata-only|mixed|none; BEHAVIOR_BLOCKERS; CANDIDATE_CHANGE_REQUIRED yes|no; FINDINGS all blockers or none; EVIDENCE; RESIDUAL_RISK.
