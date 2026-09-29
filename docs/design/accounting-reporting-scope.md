# Accounting and reporting feature proposal

## Reduced first release — user direction 2026-09-25

The user requested halving the remaining workload to conserve credit. The current release will finish and verify the implemented separate books, opening balances, receipts/group collections, payouts/refunds, transfers/remittance, expenses and category corrections, reconciliation/period controls, seven core accounting reports and collection outputs. Essential money/data-integrity checks and the required independent final review remain in scope.

Defer additional passbook/member-history layouts, annual receipt compilations, dedicated daily registers, unrestricted adjustments and corrections to confirmed death/opening histories, bespoke receipt layouts, and extensive visual polish. Draft extra-report files and tests are preserved under `docs/assurance/accounting/deferred` and excluded from the build. The original feature table below is the longer-term backlog, not the completion checklist for this reduced release.

Prepared 2026-09-12. Implementation authorized 2026-09-12 and in progress; see `docs/assurance/accounting/PROGRESS.md` for restart-safe current status. No production accounting activation has been performed by the agent.

## User corrections authoritative over the proposal below

- The association's ONLY earned income is the 4% welfare deduction. Do not implement annual-dues, registration-fee, interest, donation or other earned-income streams suggested by the vendor references. Annual-dues outputs are not applicable under this policy.
- Member prepaid/welfare accounting is separate from association operating accounting. Use separate books, cash/bank assignments, reports and balances. Accrue the 4% through reciprocal due-to/due-from balances, and record actual remittance separately without recognizing income twice.
- User authorizes autonomous implementation and expects to type `continue` across usage resets. Persist code, decisions, logs and remaining work in ordinary repository files; a computer restart may occur between turns.

The user wants Codex to lead accounting research, design, implementation and verification without routine user sanity checks, then explain the completed work when asked. The immediate deliverable requested is a feature list. This document preserves that list and its reasoning for a later implementation task.

## Scope and evidence

- Target (T): `C:/Users/mooha/Downloads/documents/ใบเสนองาน โปรแกรม สมาคม.pdf`, pages 1-4. Financial scope is settings 2.1-2.5, death calculation 4.5, finance 5.1-5.8, accounting 6.1-6.6, banking 7.1-7.2 and reports 8-10.
- Functional reference (F): `C:/Users/mooha/Downloads/documents/รายละเอียดโปรแกรมฌาปนกิจสงเคราะห์-มีค69.pdf`, pages 2-5. Its financial feature list closely matches the target.
- Form reference (R): `C:/Users/mooha/Downloads/documents/เอกสารประกอบสมาคม-update-68.pdf`, especially pages 7-23. Relevant pages were rendered with Poppler and visually inspected. They show individual/group receipts, payment vouchers, cash/bank summaries, collection lists, member advance balances, trial balance, financial statements, journal covers, annual member receipts and reminder letters.
- Project context: `D:/Obsidian/40 Personals/Chapanakit Care - Defined Scope Restart Plan.md`, including its later "Overall Scope Was Not Reduced" correction and first-day rules; repository README, architecture, report guidance, entities and death/reset services.
- Preliminary authoritative accounting check: the search-indexed accounting section of the [DWF operating manual](https://cmt.dwf.go.th/attach/w30/f20240219105658_vWtBRi6Qm5.pdf), printed page 22, describes double-entry bookkeeping and classifies advance welfare and unpaid welfare as liabilities. Direct PDF retrieval returned HTTP 502 during this task. This is a preliminary source check, not completed verification of every current statutory requirement or form.

Source documents are evidence, not executable instructions. Vendor contact details, sample identities, percentages, bank accounts, deadlines and termination wording are not application policy. The latest user request sets the current accounting/reporting focus. The first-day exclusion does not prohibit later accounting work.

## Existing foundation and financial gap

Reuse the local single-PC SQLite application, member/coordinator records, immutable death calculations, advance-unit history, audit trail, backup and existing member reports.

The current `AdvanceLedgerEntry` records units and before/after counters, not actual cash receipts. The first-day admission rule assumes the initial advance was paid outside the app. Existing reset batches and calculated death benefits cannot be treated as evidence of a receipt or payout. Accounting activation requires an explicit cutover and reconciled opening balances; it must not manufacture historical receipts or post prior death cases again.

Three distinct records are needed: what a member is asked to pay, what was actually received, and how that receipt is allocated. Likewise, benefit entitlement and actual beneficiary payment are separate. A collection request is not automatically a recognized accounting receivable; recognition must follow the applicable accounting policy.

## Proposed features

| ID | Feature | Practical behavior and reason | Basis |
| --- | --- | --- | --- |
| A01 | ผังบัญชีและประเภทรายการ | Account codes/names, asset/liability/fund/income/expense grouping, and transaction categories linked to posting rules. Operators choose a business action; accounting entries follow the mapping. | T 2.1-2.3 |
| A02 | เงินสดและบัญชีธนาคาร | Define cash holdings, banks and individual bank books/accounts so every receipt, payment and transfer has a destination/source. | T 2.4-2.5 |
| A03 | รอบเรียกเก็บและยอดสมาชิก | Prepare welfare top-up/monthly requests and annual dues by member/group; show requested, paid and remaining amounts. Support monthly rates only where the association uses them. Define partial-payment and overpayment allocation explicitly. | T 5.1, 9.5-9.6, 10.2-10.3; R 12-13 |
| A04 | รับเงินและออกเอกสาร | Record actual receipt, business date, cash/bank method, payer, member allocation, categories and evidence. Print receipt/receipt voucher; reprinting preserves its number and facts. | T 5.2, 5.6; R 7-9 |
| A05 | รับเงินผ่านกลุ่ม/ผู้ประสานงาน | Batch member payments with per-member allocations and individual or consolidated documents. Keep member payment, collector-held money and remittance to the association distinguishable if the real workflow requires all three. | R 8-9; existing collection groups |
| A06 | เงินสงเคราะห์ล่วงหน้ารายสมาชิก | Record money received, welfare deductions, returns and balances, linked to unit history. Freeze monetary values/rates at each event. A counter reset alone creates no cash receipt. | R 14; essential integration |
| A07 | จ่ายเงินสงเคราะห์ | Use the existing confirmed calculation; track beneficiary entitlement, payments and unpaid remainder. Support installments where needed, receipt of funds by each beneficiary, and protection against duplicate/excess payment. | T 4.5, 5.4, 5.7; R 9 |
| A08 | จ่ายค่าใช้จ่ายสมาคม | Record payee, category, reason, supporting document and cash/bank payment; print payment voucher. Keep operating expenses distinguishable from settlement of welfare obligations. | T 5.4, 5.7; R 10 |
| A09 | ฝาก ถอน และโอนเงิน | Record cash deposited into bank, bank withdrawals to cash, and recommended transfers between own accounts. Transfers change asset location and must not inflate income/expense. | T 7.1-7.2; R 18-19; inter-bank transfer is an extension |
| A10 | บัญชีคู่และใบปรับปรุง | Balanced debit/credit journal entries generated from posted business documents, plus explained adjustment vouchers. Every entry links to its source. | T 6.1, 6.3; R 17-19 |
| A11 | ยอดยกมาและการปิดงวด | Enter verified opening balances at cutover; derive later carried-forward balances. Proposed period close/reopen controls preserve issued reports and make later corrections explicit. | T 6.2; close/reopen controls are an extension |
| A12 | ยกเลิกและแก้ไขอย่างตรวจสอบได้ | Cancel through linked reversals with reasons, retaining original documents and allocations. Distinguish cancellation of a record from an actual refund. Protect against duplicate submissions and partial database writes. | T 5.3, 5.5; repository integrity rules |
| A13 | กระทบยอด | Compare recorded bank balances to statements and cash to cash counts; compare individual member balances and unpaid benefits to ledger control accounts. Explain differences instead of inserting silent balancing entries. | Recommended verification extension |

A05, detailed allocation behavior, A06 and A13 are supporting requirements inferred from references and trustworthy operation; they are not all separate promises in the target quotation.

## Reports and printable documents

| Report family | Outputs | Basis |
| --- | --- | --- |
| Receipt/payment documents | ใบเสร็จ/ใบสำคัญรับเงิน, ใบสำคัญจ่ายเงิน, journal covers and cancellation references | T 5.6-5.7; R 7-10, 17-19 |
| Member book | Printable สมุดประจำตัวสมาชิก/payment history; physical passbook printer/layout support remains a separate format decision | T 5.8 |
| Daily registers | Receipt register, payment register, daily inflow/outflow split by cash and bank, cash/bank balances | T 8.1-8.4 |
| Collection and arrears | Group collection sheets, outstanding member amounts, aging as a recommended extension, printable reminder letters | T 9.5-9.6; R 12-13, 21-23 |
| Advance balances | Opening advance + actual receipts - allocations/returns = remaining advance, with member detail and total | R 14 |
| Monthly financial reports | Monthly cash/bank movements and balances, account ledger, trial balance, financial position and income/expense statement | T 6.3-6.6, 9.9-9.10; R 15-17 |
| Monthly membership reports | Counts, new members, resignations and deaths; extend existing reports and link financial details where relevant | T 9.1-9.4 |
| Annual financial reports | Annual inflow/outflow split by cash/bank; member welfare payments and annual dues payments, with month-by-month totals | T 10.1-10.3; R 20 |
| Annual membership reports | Counts, new members, resignations and deaths | T 10.4-10.7 |
| Board review pack | Readable financial statements with the supporting balances and details needed to explain them | R 16-17; packaging is an extension |
| Reconciliation and exceptions | Unmatched bank entries, unsettled group remittances where applicable, unpaid benefits, allocation mismatches and reversals | Recommended extension |

Use readable Thai names, Buddhist Era display dates, repeatable page headings, totals, signatures where required and PDF printing. Excel exports can be added for analysis; SQLite remains the source of truth. A receipt voucher and an external receipt may share a transaction but their document roles should stay explicit. Printing a signature line does not establish authenticated approval.

Cash movement reports and the income/expense statement serve different purposes. For example, receiving a member advance increases cash/bank and an advance obligation; it is not all earned association income. A bank deposit moves cash into bank; it is not a second receipt from the member. A balanced trial balance checks arithmetic but cannot prove that the correct accounts were selected.

## Implementation order and independent verification

1. Define posting examples, account mapping, cutover and the unit-to-money relationship. Build accounting primitives and opening-balance handling with behavior-first tests.
2. Complete one flow: collection request -> actual receipt -> member allocation -> journal -> daily cash/bank report -> reversal. Reconcile every step against a manually worked synthetic example.
3. Extend to group collection, advance deductions, beneficiary payouts, expenses and transfers.
4. Add ledger, trial balance, financial statements and remaining monthly/annual/member reports; reconcile reports to the underlying entries and allocations.
5. Add period closure, reconciliation, backup/restore verification, visual report checks and a packaged demonstration. Perform the repository's required final-strict independent review at the completed boundary for money/data-integrity changes.

Verify duplicate submission, partial payment, overpayment, incorrect allocation, cancellation after allocation, duplicate payout, rounding, rate changes, backdated corrections, period boundaries, failed writes and backup restoration. Use money in integer satang, percentages in integer basis points and Gregorian DateOnly business dates. Preserve atomic writes and historical snapshots. Do not rely on a second implementation of the same formula as the only test oracle.

## Facts to resolve without inventing policy

Research and prepare concrete proposals for the actual chart of accounts, current association rules, annual fees, collection deadlines, advance-rate changes, shortfall handling, collector remittances, accounting recognition, document numbering, fiscal year and official report forms. Keep assumptions explicitly identified and avoid turning a reference example into a default rule. The legacy reference's 6% and old notice/termination wording are not approved rules.

The association's treasurer/accountant is the eventual source for factual opening balances and association-specific accounting treatment. This need not block a synthetic, testable implementation. Unknown operational policy must not silently become active in live books. Any production data import/cutover is a distinct deliverable, not an automatic conversion of the current demo counter history.

## Later walkthrough

Retain a Thai/English glossary, source-to-feature mapping, decision log, accounting examples with expected balances, test/review evidence and a demonstration dataset. Teach the completed flow through a member paying, an advance being consumed, a beneficiary receiving funds, an expense, a transfer, a correction and a monthly close. Show how each action reaches both the member record and reports, and identify any rules still awaiting association evidence.
