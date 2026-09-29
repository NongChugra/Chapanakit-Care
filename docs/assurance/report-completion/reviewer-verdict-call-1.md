VERDICT: fix-first
AUDIT_COMPLETENESS: complete
FINDING_CLASS: behavior
BEHAVIOR_BLOCKERS: F1
CANDIDATE_CHANGE_REQUIRED: yes

F1: All/group, monthly and Sak One sequence columns split 1000 into 100 and 0 on separate lines. The 20/18-point columns are narrower than four digits plus padding. This violates no-split-numbers acceptance for ordinary reports reaching 1,000 members. Existing dense fixtures stop at 80 and omit sequence-number geometry checks. Reviewer reproduced in memory against the published DLL, identical to Release, without file changes. Accommodate supported count range and add a split-sensitive regression check.

Evidence: full nine-file source/doc audit, 188-path manifest/readiness replay, live call-1 lock/start journal, 145-test and download receipts, all references and PDF samples, independent hashes/bounds for 37 pages. No additional blockers. Residual limitations are historical group reconstruction and unavailable schema fields declared in packet. Actual child Sol/max proof: reviewer-runtime-call-1.json; full returned verdict in reviewer rollout.
