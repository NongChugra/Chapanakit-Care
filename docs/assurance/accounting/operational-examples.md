# Independent accounting examples and integration decisions

Status: expected results for implementation/tests, not executed test evidence.

## Separate-book example (all amounts in satang)

Four members each have documented prepaid money 27000. Welfare cash is 108000; welfare advance liability is 108000. Association opens at zero. One member dies, leaving three contributors. Current rate 900 and 4% whole-baht-floor deduction produce gross 2700, fee 100, net benefit 2600. Return the deceased's recorded 27000 advance, making benefit payable 29600 (14800 to each of two beneficiaries).

Welfare accrual: debit member advances 29700, credit benefit payable 29600, credit fee due to association 100. Association accrual: debit fee due from welfare 100, credit 4% deduction income 100. Welfare remaining advances: 78300. No cash moves on accrual.

Pay beneficiaries: welfare debit payable 29600, credit cash 29600. Cash then 78400. Remit fee: welfare debit due-to 100, credit cash 100; association debit cash 100, credit due-from 100. End welfare cash 78300 = advances 78300. End association cash100 = income100. Income stays100 after remittance. An association expense60 leaves cash40 and net result40.

## Partial funding and receipts

Member has 451 paid advance and owes a contribution900. Consume only451 of advance and recognize449 shortfall receivable. A later receipt1000 clears449 of receivable and adds551 advance. Cash increases1000; earned income remains0. A second identical receipt token must not double cash; changed payload with the same token must fail.

Funded monetary balances do not get revalued when the per-person rate changes. On accounting activation monetary state is explicit. Unit counts displayed by the old workflow become a projection using the current rate, while recorded money remains exact to satang. Existing stored snapshots remain immutable.

## Nonpay and negative benefit boundary

Nonpay death creates no fee or benefit accrual. Existing funded money is still an obligation in the welfare book; zeroing the administrative counter is not a refund or forfeiture. Display it as an unsettled deceased-member advance, requiring an explicit supported refund/correction. Do not invent an automatic forfeiture rule.

If a deceased member's shortfall exceeds the available net benefit, do not post a negative payout or silently forgive the balance. Reject financial confirmation with an actionable explanation until the balance is settled or a supported adjustment is recorded. This is a safe exception boundary rather than an invented debt-waiver policy.

## Financial chronology

For the first accounting version, reject operational postings before the latest already-posted business date for the affected book. Period locks alone do not prevent a backdated withdrawal from invalidating a previously checked balance. Opening dates are explicit. Corrections are dated in the current open chronology and refer to the original document. Reports still support historical as-of dates. This limitation should be disclosed in the guide, and can later be relaxed only with historical balance validation.

## Reversals and downstream use

Receipt reversal is allowed only if the original allocation can be undone without making funded advance negative or making a settled shortfall inconsistent. A payout reversal restores payable and cash; a recorded cancellation does not prove physical cash was returned. Linked fee remittance/accrual reversals must reverse both books atomically. Accrual reversal requires no payout/remittance downstream and cannot directly unconfirm a death; no automatic death void workflow is introduced by generic journal reversal.

## Billing and reporting

Collection requests are instructions to collect, not cash or income. Each request records member/group snapshots, assessment period, due date and requested amount. Allocations from actual receipts reference requests. Paid/outstanding calculations include reversal effects. Reminder letters are generated only for actual outstanding requests, never auto-sent and never automatic membership termination. Annual-dues reports are explicitly not applicable under the current sole-income policy.
