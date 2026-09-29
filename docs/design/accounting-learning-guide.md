# Understanding the accounting module

Guide to the reduced accounting release. See `docs/assurance/accounting/PROGRESS.md` for current acceptance status and `reduced-release-verification.md` in that directory for actual verification evidence.

## Two books

**บัญชีเงินสงเคราะห์สมาชิก (welfare book)** records member advances, welfare obligations, member contribution shortfalls and the welfare bank/cash holdings. A member deposit increases money held and the obligation to that member; it is not association income.

**บัญชีดำเนินงานสมาคม (association book)** records the 4% deduction income and operating expenses. Its bank/cash holdings are separate. The user has confirmed that this deduction is the association's only earned income.

## Terms used by the application

| Term | Meaning here |
| --- | --- |
| ใบเรียกเก็บ | A request for payment. It does not prove money arrived. |
| ใบเสร็จ / ใบสำคัญรับเงิน | Evidence recorded for an actual receipt, tied to its member allocation and cash/bank account. |
| เงินสงเคราะห์ล่วงหน้า | Money already held for a member's future welfare obligations. |
| ยอดค้างชำระ | An amount outstanding. A collection request and an accounting receivable can have different recognition rules. |
| เงินสงเคราะห์ค้างจ่าย | A recognized benefit still owed to a beneficiary. |
| เงินค้างโอนระหว่างบัญชี | Money due between the welfare and association books, such as the recognized 4% fee awaiting remittance. |
| เดบิต / เครดิต | The two sides of a journal. Credit does not always mean income, and debit does not always mean expense. |
| บัญชีแยกประเภท | All posted movements for one account, with its resulting balance. |
| งบทดลอง | A listing used to check that recorded debits and credits balance. It cannot by itself prove correct classification. |
| งบแสดงฐานะทางการเงิน | What the book holds, what it owes, and its remaining accumulated funds at a date. |
| งบรายได้ค่าใช้จ่าย | Earned income minus operating expenses during a period. It is not the same as cash received minus cash paid. |
| กระทบยอด | Comparing system balances to external evidence or detailed member records and explaining differences. |
| กลับรายการ | A linked correcting entry preserving the original. It is not automatic evidence that physical money was returned. |

## Follow one case

The independent, hand-worked example is in `docs/assurance/accounting/operational-examples.md`. Four members have paid 270 baht each into the welfare book. A payable death leaves three contributors at 9 baht each: 27 baht gross, 1 baht fee using the current whole-baht rounding rule, 26 baht net, plus the deceased's 270 baht prepaid balance. Total benefit is296 baht.

At confirmation, welfare records296 baht payable to beneficiaries and1 baht due to the association. Association records1 baht receivable and1 baht income. These are accounting events; no cash has moved yet. Paying the beneficiaries reduces welfare cash and payable. Transferring the1 baht fee reduces welfare cash and the inter-book balances, and increases association cash. It does not create another1 baht of income.

## Why opening balances require evidence

The earlier application assumed initial payments occurred outside it and stored administrative units. A unit count is not a historical receipt. The accounting start date must establish actual opening balances supported by records. Existing member and death history remains intact, but the application must not invent receipts or repeat historical payouts.

## Resignation and actual refund

Verified in the 2026-09-23 focused tests: a member with 9.01 baht remaining keeps that exact balance when resigning. The membership status changes, but the welfare book still holds 9.01 baht and still owes the member 9.01 baht. The old administrative counter is closed; it is no longer a measure of money owed to this former member.

The treasurer records the actual payment separately under **จ่ายคืนเงินล่วงหน้า**. A 9.00 baht payment leaves 0.01 baht payable. Repeating the same submitted request does not pay twice, and a new payment above the remaining balance is rejected. The transaction requires a source cash/bank account and evidence identifying the actual recipient/payment. Refunds reduce a liability; they are not an association operating expense.

If a resigned or deceased member owes money instead, **รับเงินสมาชิก** can record debt settlement up to the outstanding debt. It cannot create fresh prepaid coverage for a member whose membership has ended. Correcting a mistaken receipt or refund preserves the member's ended status and does not reopen the administrative counter.

## Correction boundary currently implemented

The voucher screen can reverse an eligible receipt, refund, expense, cash transfer, beneficiary payment or fee remittance only while it is the latest journal in each affected book. A remittance reversal corrects both books together. The original document remains available and is marked reversed on reissue. Opening balances and death accruals require a dedicated correction process, deferred from this release; they must not be rewritten in place. This conservative boundary avoids undoing money already used by a later payment. Older expense categories can be corrected through the separate reclassification form.

## Reconciliation and closing a period

The **ตั้งค่า / กระทบยอด / ปิดงวด** page works on the selected book. Enter the actual bank statement balance or counted cash balance and the evidence date. For example, if the ledger says 100.00 baht while the statement says 99.50 baht, the saved result is a difference of -0.50 baht. The system does not invent an expense or change cash to conceal that difference. Investigate and record the supported correction separately.

Closing a period prevents new postings within its dates. If a correction is needed, reopen that exact period with a reason, record and verify the correction, then close it again. Each close/reopen action keeps its own audit reason. Closing is not a substitute for checking member-detail totals, unpaid benefits and external bank evidence.

## Planned demonstration checklist

- Explicitly set up both books and verify opening totals.
- Prepare a group collection and show that cash does not change.
- Record a partial payment and explain allocation to shortfall/advance.
- Confirm a payable death and inspect both books' linked journals.
- Pay each beneficiary, then remit the4% fee.
- Record an association operating expense.
- Explain and reverse an erroneous eligible transaction.
- Compare bank/cash evidence, close a period and generate separate monthly reports.

No reminder is automatically sent and no member is automatically terminated because a sample letter contained that wording.

## Reduced release boundary (2026-09-25)

The accounting menu provides separate book overviews, explicit opening balances, member receipts and group collections, beneficiary payments, member refunds, cash/bank transfers, 4% remittance, association expenses, vouchers, reconciliation and period controls. Collection requests themselves do not create cash or paid balances. PDF/CSV reports cover trial balance, journal, general ledger, income/expense, financial position, member balances, unpaid beneficiaries and collection outputs.

Expense category corrections may reclassify a supported older expense without changing total expense or cash. Other reversals retain the latest-document restriction above. Corrections to confirmed deaths/opening balances and unrestricted adjustments are deferred. Extra member-history/passbook layouts, annual receipt compilations, dedicated daily registers and bespoke receipt designs are also deferred at the user's request to halve remaining work.

The per-member welfare contribution rate must be set before activating the welfare book. This reduced release locks that rate after activation so administrative unit counts remain consistent with monetary balances; a future rate-change workflow must recalculate those projections with an audit history. Other settings remain editable within their existing validation rules.

Before actual cutover, enter verified opening cash, banks and member/beneficiary liabilities for each book; administrative unit counters are not payment evidence. The agent's packaged demonstration uses an isolated database and does not activate production books. These reports are operational accounting outputs; association-specific statutory submission requirements have not been certified.
