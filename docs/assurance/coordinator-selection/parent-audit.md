# Parent adversarial audit

Scope: one coordinator-selection workflow with shared current-role reads in three pages. No report formatting or financial calculation changes. Migration creates two tables; existing status services only gain an atomic role-ending call.

| Invariant / counterexample | Reachable path | Protection and sensitivity evidence |
| --- | --- | --- |
| Same person submitted to chair and leader positions | Coordinator POST/service; two concurrent SQLite contexts | Filtered unique member index plus transaction validation. Workflow rejects second role; direct-SQL uniqueness and competing-request tests exercise independent boundaries. |
| Wrong group, nonexistent/unknown key, deceased/resigned/archived member | Forged POST or status changed after searching | Server reloads member and validates exact group and current eligibility. Picker is convenience, not the enforcing boundary. Negative theories were RED before implementation. |
| Appoint silently replaces an incumbent; stale end/replace; old empty form reused after end | Multiple browser tabs or repeated submission | Explicit action, expected incumbent and monotonic persisted position version. Vacancy row is retained. Tests assert unchanged holder/history after rejection. |
| Rename a member after appointment, edit/delete history | Member edit or tracked EF event mutation | History stores name/number snapshots; tracked modifications/deletions are rejected. Tests observe old name and unchanged persisted history. |
| Move a current leader to another group or make holder ineligible | Member edit, resignation/death, direct tracked status/archive change | Member editor blocks group move before mutation. Persistence guard checks relevant member changes. Lifecycle services append end within their existing transaction. Ending role permits subsequent group change. |
| History write fails after projection/member/ledger changes | Replace, resignation, payable death | Injected SQLite abort triggers force rollback. Tests inspect holder, events, member status/balances, survivor balance, ledger and death snapshots after clearing tracked state. |
| Existing database upgrade or backup loses member/role data | Additive migration; backup and restored SQLite | Prior migration upgraded with seeded member; identity/number/balance/timestamp preserved. Restored roles, history and migration chain checked. Model drift assertion was RED before scaffolding. |
| New FK prevents existing demo reset | Existing ClearMembersAsync transaction | Events and positions removed before members. Test was RED on FK error and passes with explicit dependency cleanup. |
| GET button action/POST binding differs from direct service | Actual rendered Razor forms | Worker HTTP tests and parent browser checks cover actual controls, antiforgery and redirect flow. See UI evidence and candidate receipt. |
| Role column conflicts with membership status or saved preferences | Library plus dashboard | Derived role view is separate from Member.Status; parent checks column layout, filtering, persistence and shared holder display in the browser. |

Raw external database edits are not an application workflow. The existing explicit demo reset uses bulk deletes to clear demo history; ordinary role history has no edit/delete action. Group identity is the exact existing GroupNo, not inferred from address strings. Future reports and historical group reconstruction remain deferred.

Before review, reconcile every acceptance item with candidate receipt, inspect the complete changed-since-base scope, and set readiness only after all applicable checks pass. This file alone is not a review-ready assertion.

## Final reconciliation

All matrix rows pass against candidate-receipt.md and the frozen candidate. All eight ledger acceptance criteria are met within this source delivery boundary. Product and architecture contradictions: none. Unresolved assumptions: none. Fix-induced regression checks: full 128-test suite and published browser/report checks pass. Candidate scope reconciled: all 29 changed-since-task-base paths, including untracked source/tests/design and additive migration designer/snapshot; unrelated baseline files remain byte-identical. PARENT_ADVERSARIAL_READY: yes. Reviewability: pass, one role-selection invariant family with explicit lifecycle, backup, migration and shared-view interactions.
