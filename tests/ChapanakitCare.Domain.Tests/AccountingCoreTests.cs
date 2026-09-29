using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingCoreTests
{
    [Fact]
    public async Task Catalog_separates_welfare_from_association_and_only_exposes_the_4_percent_income_account()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);

        await setup.EnsureCatalogAsync();

        var books = await store.Context.AccountingBooks.OrderBy(x => x.Code).ToListAsync();
        var accounts = await store.Context.AccountingAccounts.ToListAsync();

        Assert.Equal([AccountingBookCode.Welfare, AccountingBookCode.Association], books.Select(x => x.Code).Order().ToArray());
        var welfare = Assert.Single(books, x => x.Code == AccountingBookCode.Welfare);
        var association = Assert.Single(books, x => x.Code == AccountingBookCode.Association);
        Assert.NotEqual(welfare.Id, association.Id);
        Assert.Contains(accounts, x => x.BookId == welfare.Id && x.Code == "2000" && x.Role == AccountingAccountRole.MemberAdvanceLiability);
        Assert.Contains(accounts, x => x.BookId == welfare.Id && x.Code == "2200" && x.Role == AccountingAccountRole.FeeDueToAssociation);
        Assert.Contains(accounts, x => x.BookId == association.Id && x.Code == "1300" && x.Role == AccountingAccountRole.DueFromWelfare);
        var income = Assert.Single(accounts, x => x.AccountType == AccountingAccountType.Income);
        Assert.Equal(association.Id, income.BookId);
        Assert.Equal("4000", income.Code);
        Assert.Equal(AccountingAccountRole.WelfareDeductionIncome, income.Role);
    }

    [Fact]
    public async Task Balanced_welfare_posting_records_dimensions_and_audit_without_creating_an_association_journal()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var member = TestData.Member();
        store.Context.Members.Add(member);
        await store.Context.SaveChangesAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await setup.ActivateBookAsync(new AccountingBookActivation(
            AccountingBookCode.Welfare,
            new DateOnly(2026, 9, 1),
            "Opening balance is explicitly documented as zero.",
            Actor()));
        var posting = new AccountingPostingService(store.Context);

        var result = await posting.PostAsync(new AccountingPostRequest(
            AccountingBookCode.Welfare,
            "advance_receipt",
            "รับเงินสงเคราะห์ล่วงหน้าสมาชิก",
            new DateOnly(2026, 9, 12),
            "advance-receipt-0001",
            "request-fingerprint-advance-receipt-0001",
            Actor(),
            [
                new("1000", 9_000, 0, new AccountingDimensions(
                    MemberId: member.Id,
                    PartySnapshot: "ทดสอบ สมาชิก",
                    DescriptionSnapshot: "รับเงินสด")),
                new("2000", 0, 9_000, new AccountingDimensions(
                    MemberId: member.Id,
                    PartySnapshot: "ทดสอบ สมาชิก",
                    DescriptionSnapshot: "เพิ่มเงินสงเคราะห์ล่วงหน้า"))
            ]));

        var journal = await store.Context.AccountingJournals.SingleAsync(x => x.Id == result.JournalId);
        var welfare = await store.Context.AccountingBooks.SingleAsync(x => x.Code == AccountingBookCode.Welfare);
        var lines = await store.Context.AccountingJournalLines.Where(x => x.JournalId == journal.Id)
            .OrderBy(x => x.LineNo).ToListAsync();

        Assert.False(result.WasReplayed);
        Assert.Equal(welfare.Id, journal.BookId);
        Assert.Equal("advance_receipt", journal.VoucherType);
        Assert.Equal(new DateOnly(2026, 9, 12), journal.BusinessDate);
        Assert.Equal(2, lines.Count);
        Assert.Equal(9_000, lines.Sum(x => x.DebitSatang));
        Assert.Equal(9_000, lines.Sum(x => x.CreditSatang));
        Assert.All(lines, line => Assert.Equal(member.Id, line.MemberId));
        Assert.All(lines, line => Assert.Equal("ทดสอบ สมาชิก", line.PartySnapshot));
        Assert.Empty(await store.Context.AccountingJournals.Where(x => x.BookId != welfare.Id).ToListAsync());
        var audit = await store.Context.AccountingPostingAudits.SingleAsync(x => x.JournalId == journal.Id);
        Assert.Equal("posted", audit.Action);
        Assert.Equal(journal.OperationId, audit.OperationId);
    }

    [Fact]
    public async Task Posting_rejects_a_negative_amount_even_when_the_other_side_is_positive()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await setup.ActivateBookAsync(new AccountingBookActivation(
            AccountingBookCode.Welfare,
            new DateOnly(2026, 9, 1),
            "Opening balance is explicitly documented as zero.",
            Actor()));
        var posting = new AccountingPostingService(store.Context);

        var exception = await Assert.ThrowsAsync<AccountingValidationException>(() => posting.PostAsync(new AccountingPostRequest(
            AccountingBookCode.Welfare,
            "invalid_amount",
            "ต้องไม่ยอมรับจำนวนติดลบ",
            new DateOnly(2026, 9, 12),
            "invalid-negative-amount-0001",
            "request-fingerprint-invalid-negative-amount-0001",
            Actor(),
            [
                new("1000", -100, 100),
                new("2000", 200, 0)
            ])));

        Assert.Contains("จำนวนบวก", exception.Message);
        Assert.Empty(await store.Context.AccountingJournals.ToListAsync());
    }

    [Fact]
    public async Task Idempotency_tokens_are_scoped_to_their_separate_accounting_books()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        await ActivateBookAsync(setup, AccountingBookCode.Association);
        var posting = new AccountingPostingService(store.Context);

        var welfare = await posting.PostAsync(BalancedCashOpening(
            AccountingBookCode.Welfare,
            "shared-business-attempt-0001",
            "same-payload-for-separate-books"));
        var association = await posting.PostAsync(BalancedCashOpening(
            AccountingBookCode.Association,
            "shared-business-attempt-0001",
            "same-payload-for-separate-books"));

        Assert.NotEqual(welfare.JournalId, association.JournalId);
        Assert.False(welfare.WasReplayed);
        Assert.False(association.WasReplayed);
        var journals = await store.Context.AccountingJournals.OrderBy(x => x.JournalNumber).ToListAsync();
        Assert.Equal(2, journals.Count);
        Assert.Equal(2, journals.Select(x => x.BookId).Distinct().Count());
        Assert.All(journals, x => Assert.Equal("shared-business-attempt-0001", x.RequestToken));
    }

    [Fact]
    public async Task Linked_fee_accrual_posts_one_balanced_journal_per_book_as_one_atomic_operation()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        await ActivateBookAsync(setup, AccountingBookCode.Association);
        var posting = new AccountingPostingService(store.Context);
        var operationId = Guid.NewGuid();

        var result = await posting.PostLinkedAsync(new AccountingLinkedPostRequest(
            operationId,
            new AccountingPostRequest(
                AccountingBookCode.Welfare,
                "fee_accrual",
                "ตั้งหนี้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                new DateOnly(2026, 9, 12),
                "fee-accrual-welfare-0001",
                "fee-accrual-welfare-payload-0001",
                Actor(),
                [new("1000", 100, 0), new("2200", 0, 100)]),
            new AccountingPostRequest(
                AccountingBookCode.Association,
                "fee_accrual",
                "รับรู้รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                new DateOnly(2026, 9, 12),
                "fee-accrual-association-0001",
                "fee-accrual-association-payload-0001",
                Actor(),
                [new("1300", 100, 0), new("4000", 0, 100)])));

        Assert.False(result.WasReplayed);
        Assert.NotEqual(result.WelfarePosting.JournalId, result.AssociationPosting.JournalId);
        var journals = await store.Context.AccountingJournals.OrderBy(x => x.JournalNumber).ToListAsync();
        Assert.Equal(2, journals.Count);
        Assert.All(journals, journal => Assert.Equal(operationId, journal.LinkedOperationId));
        Assert.Equal(
            [AccountingBookCode.Welfare, AccountingBookCode.Association],
            (from journal in journals
             join book in store.Context.AccountingBooks on journal.BookId equals book.Id
             orderby book.Code
             select book.Code).ToArray());
        Assert.Equal(2, await store.Context.AccountingPostingAudits.CountAsync());
    }

    [Fact]
    public async Task Failed_linked_posting_does_not_leave_the_first_book_persisted_when_the_caller_commits_its_ambient_transaction()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        await ActivateBookAsync(setup, AccountingBookCode.Association);
        var posting = new AccountingPostingService(store.Context);

        await using (var transaction = await store.Context.Database.BeginTransactionAsync())
        {
            var failure = await Assert.ThrowsAsync<AccountingValidationException>(() => posting.PostLinkedAsync(new AccountingLinkedPostRequest(
                Guid.NewGuid(),
                new AccountingPostRequest(
                    AccountingBookCode.Welfare,
                    "fee_accrual",
                    "ตั้งหนี้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                    new DateOnly(2026, 9, 12),
                    "failed-fee-accrual-welfare-0001",
                    "failed-fee-accrual-welfare-payload-0001",
                    Actor(),
                    [new("1000", 100, 0), new("2200", 0, 100)]),
                new AccountingPostRequest(
                    AccountingBookCode.Association,
                    "fee_accrual",
                    "รับรู้รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                    new DateOnly(2026, 9, 12),
                    "failed-fee-accrual-association-0001",
                    "failed-fee-accrual-association-payload-0001",
                    Actor(),
                    [new("9999", 100, 0), new("4000", 0, 100)]))));

            Assert.Contains("ไม่พบบัญชี", failure.Message);
            await transaction.CommitAsync();
        }

        store.Context.ChangeTracker.Clear();
        Assert.Empty(await store.Context.AccountingJournals.ToListAsync());
        Assert.Empty(await store.Context.AccountingJournalLines.ToListAsync());

        var success = await posting.PostLinkedAsync(new AccountingLinkedPostRequest(
            Guid.NewGuid(),
            new AccountingPostRequest(
                AccountingBookCode.Welfare,
                "fee_accrual",
                "ตั้งหนี้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                new DateOnly(2026, 9, 12),
                "successful-fee-accrual-welfare-0001",
                "successful-fee-accrual-welfare-payload-0001",
                Actor(),
                [new("1000", 100, 0), new("2200", 0, 100)]),
            new AccountingPostRequest(
                AccountingBookCode.Association,
                "fee_accrual",
                "รับรู้รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                new DateOnly(2026, 9, 12),
                "successful-fee-accrual-association-0001",
                "successful-fee-accrual-association-payload-0001",
                Actor(),
                [new("1300", 100, 0), new("4000", 0, 100)])));

        Assert.Equal("W-00000001", success.WelfarePosting.JournalNumber);
        Assert.Equal("A-00000001", success.AssociationPosting.JournalNumber);
    }

    [Fact]
    public async Task Failed_linked_posting_restores_the_callers_pending_changes_and_accounting_tracker_before_the_ambient_transaction_continues()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var member = TestData.Member();
        store.Context.Members.Add(member);
        await store.Context.SaveChangesAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        await ActivateBookAsync(setup, AccountingBookCode.Association);
        var posting = new AccountingPostingService(store.Context);
        member.FirstName = "แก้ไขโดยผู้เรียก";

        await using (var transaction = await store.Context.Database.BeginTransactionAsync())
        {
            await Assert.ThrowsAsync<AccountingValidationException>(() => posting.PostLinkedAsync(new AccountingLinkedPostRequest(
                Guid.NewGuid(),
                new AccountingPostRequest(
                    AccountingBookCode.Welfare,
                    "fee_accrual",
                    "ตั้งหนี้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                    new DateOnly(2026, 9, 12),
                    "tracked-failure-welfare-0001",
                    "tracked-failure-welfare-payload-0001",
                    Actor(),
                    [new("1000", 100, 0), new("2200", 0, 100)]),
                new AccountingPostRequest(
                    AccountingBookCode.Association,
                    "fee_accrual",
                    "รับรู้รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                    new DateOnly(2026, 9, 12),
                    "tracked-failure-association-0001",
                    "tracked-failure-association-payload-0001",
                    Actor(),
                    [new("9999", 100, 0), new("4000", 0, 100)]))));

            Assert.Equal(EntityState.Modified, store.Context.Entry(member).State);
            Assert.Empty(store.Context.ChangeTracker.Entries<AccountingJournal>());
            Assert.Empty(store.Context.ChangeTracker.Entries<AccountingJournalLine>());
            Assert.Empty(store.Context.ChangeTracker.Entries<AccountingPostingAudit>());
            var welfare = await store.Context.AccountingBooks.SingleAsync(x => x.Code == AccountingBookCode.Welfare);
            var association = await store.Context.AccountingBooks.SingleAsync(x => x.Code == AccountingBookCode.Association);
            Assert.Equal(1, welfare.NextJournalNumber);
            Assert.Equal(1, association.NextJournalNumber);

            await store.Context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        store.Context.ChangeTracker.Clear();
        Assert.Equal("แก้ไขโดยผู้เรียก", (await store.Context.Members.SingleAsync(x => x.Id == member.Id)).FirstName);
        Assert.Empty(await store.Context.AccountingJournals.ToListAsync());
    }

    [Fact]
    public async Task Account_setup_creates_book_specific_banks_and_an_association_expense_category_without_another_income_account()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();

        var welfareBank = await setup.AddBankAccountAsync(new AccountingBankAccountSetup(
            AccountingBookCode.Welfare,
            "1101",
            "บัญชีธนาคารสวัสดิการหลัก",
            "ธนาคารตัวอย่าง สาขากลาง",
            Actor()));
        var associationBank = await setup.AddBankAccountAsync(new AccountingBankAccountSetup(
            AccountingBookCode.Association,
            "1101",
            "บัญชีธนาคารสมาคมหลัก",
            "ธนาคารตัวอย่าง สาขากลาง",
            Actor()));
        var expense = await setup.AddExpenseCategoryAsync(new AccountingExpenseCategorySetup(
            "5001",
            "ค่าวัสดุสำนักงาน",
            Actor()));

        Assert.NotEqual(welfareBank.BookId, associationBank.BookId);
        Assert.All([welfareBank, associationBank], account =>
        {
            Assert.Equal(AccountingAccountType.Asset, account.AccountType);
            Assert.Equal(AccountingAccountRole.Bank, account.Role);
            Assert.Equal(AccountingNormalBalance.Debit, account.NormalBalance);
            Assert.True(account.IsBankAccount);
        });
        Assert.Equal(AccountingBookCode.Association,
            (await store.Context.AccountingBooks.SingleAsync(x => x.Id == expense.BookId)).Code);
        Assert.Equal(AccountingAccountType.Expense, expense.AccountType);
        Assert.Equal(AccountingAccountRole.OperatingExpense, expense.Role);
        Assert.False(expense.IsBankAccount);
        var income = await store.Context.AccountingAccounts.Where(x => x.AccountType == AccountingAccountType.Income).ToListAsync();
        Assert.Single(income);
        Assert.Equal("4000", income[0].Code);

        await Assert.ThrowsAsync<AccountingValidationException>(() => setup.AddBankAccountAsync(new AccountingBankAccountSetup(
            AccountingBookCode.Welfare,
            "1101",
            "บัญชีซ้ำ",
            "ธนาคารตัวอย่าง",
            Actor())));
    }

    [Fact]
    public async Task Closing_a_book_period_blocks_posting_until_a_documented_reopen()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        var posting = new AccountingPostingService(store.Context);
        var closure = new AccountingPeriodCommand(
            AccountingBookCode.Welfare,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            "มติคณะกรรมการปิดงวดกันยายน",
            Actor());

        await setup.ClosePeriodAsync(closure);

        var closed = await store.Context.AccountingPeriods.SingleAsync();
        Assert.Equal(AccountingPeriodStatus.Closed, closed.Status);
        Assert.Equal(closure.Evidence, closed.ClosureEvidence);
        Assert.NotNull(closed.ClosedAtUtc);
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => posting.PostAsync(BalancedReceipt(
            "closed-period-receipt-0001",
            "closed-period-receipt-payload-0001")));
        Assert.Empty(await store.Context.AccountingJournals.ToListAsync());

        await setup.ReopenPeriodAsync(closure with { Evidence = "มติแก้ไขรายการรับเงินเดือนกันยายน" });

        var reopened = await store.Context.AccountingPeriods.SingleAsync();
        Assert.Equal(AccountingPeriodStatus.Open, reopened.Status);
        Assert.Equal(closure.Evidence, reopened.ClosureEvidence);
        Assert.Equal("มติแก้ไขรายการรับเงินเดือนกันยายน", reopened.ReopenReason);
        Assert.NotNull(reopened.ReopenedAtUtc);
        var result = await posting.PostAsync(BalancedReceipt(
            "reopened-period-receipt-0001",
            "reopened-period-receipt-payload-0001"));
        Assert.Equal("W-00000001", result.JournalNumber);
    }

    [Fact]
    public async Task Reversal_posts_the_exact_opposite_lines_replays_its_request_and_rejects_a_second_reversal()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        var posting = new AccountingPostingService(store.Context);
        var original = await posting.PostAsync(BalancedReceipt(
            "reversible-receipt-0001",
            "reversible-receipt-payload-0001"));
        var reversal = new AccountingReverseRequest(
            original.JournalId,
            new DateOnly(2026, 9, 13),
            "reverse-receipt-0001",
            "reverse-receipt-payload-0001",
            Actor(),
            "ยกเลิกรายการรับเงินที่บันทึกผิด");

        var result = await posting.ReverseAsync(reversal);
        var replay = await posting.ReverseAsync(reversal);

        Assert.Equal("W-00000002", result.JournalNumber);
        Assert.True(replay.WasReplayed);
        Assert.Equal(result.JournalId, replay.JournalId);
        var reverseJournal = await store.Context.AccountingJournals.SingleAsync(x => x.Id == result.JournalId);
        Assert.Equal(original.JournalId, reverseJournal.ReversesJournalId);
        var originalLines = await store.Context.AccountingJournalLines.Where(x => x.JournalId == original.JournalId)
            .OrderBy(x => x.LineNo).ToListAsync();
        var reverseLines = await store.Context.AccountingJournalLines.Where(x => x.JournalId == result.JournalId)
            .OrderBy(x => x.LineNo).ToListAsync();
        Assert.Equal(originalLines.Select(x => x.AccountId), reverseLines.Select(x => x.AccountId));
        Assert.Equal(originalLines.Select(x => x.CreditSatang), reverseLines.Select(x => x.DebitSatang));
        Assert.Equal(originalLines.Select(x => x.DebitSatang), reverseLines.Select(x => x.CreditSatang));

        await Assert.ThrowsAsync<AccountingValidationException>(() => posting.ReverseAsync(reversal with
        {
            RequestToken = "reverse-receipt-0002",
            RequestFingerprint = "reverse-receipt-payload-0002"
        }));
        Assert.Equal(2, await store.Context.AccountingJournals.CountAsync());
    }

    [Fact]
    public async Task Reversal_rejects_opening_and_linked_accrual_journals_that_need_their_own_corrective_workflow()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var setup = new AccountingSetupService(store.Context);
        await setup.EnsureCatalogAsync();
        await ActivateBookAsync(setup, AccountingBookCode.Welfare);
        await ActivateBookAsync(setup, AccountingBookCode.Association);
        var posting = new AccountingPostingService(store.Context);
        var opening = await posting.PostAsync(BalancedCashOpening(
            AccountingBookCode.Welfare,
            "opening-reversal-0001",
            "opening-reversal-payload-0001"));
        var linked = await posting.PostLinkedAsync(new AccountingLinkedPostRequest(
            Guid.NewGuid(),
            new AccountingPostRequest(
                AccountingBookCode.Welfare,
                "fee_accrual",
                "ตั้งหนี้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                new DateOnly(2026, 9, 13),
                "linked-reversal-welfare-0001",
                "linked-reversal-welfare-payload-0001",
                Actor(),
                [new("1000", 100, 0), new("2200", 0, 100)]),
            new AccountingPostRequest(
                AccountingBookCode.Association,
                "fee_accrual",
                "รับรู้รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4",
                new DateOnly(2026, 9, 13),
                "linked-reversal-association-0001",
                "linked-reversal-association-payload-0001",
                Actor(),
                [new("1300", 100, 0), new("4000", 0, 100)])));

        await Assert.ThrowsAsync<AccountingValidationException>(() => posting.ReverseAsync(new AccountingReverseRequest(
            opening.JournalId,
            new DateOnly(2026, 9, 14),
            "opening-reversal-request-0001",
            "opening-reversal-request-payload-0001",
            Actor(),
            "ไม่อนุญาตให้กลับยอดยกมา")));
        await Assert.ThrowsAsync<AccountingValidationException>(() => posting.ReverseAsync(new AccountingReverseRequest(
            linked.WelfarePosting.JournalId,
            new DateOnly(2026, 9, 14),
            "linked-reversal-request-0001",
            "linked-reversal-request-payload-0001",
            Actor(),
            "ต้องกลับรายการร่วมทั้งสองสมุด")));
        Assert.Equal(3, await store.Context.AccountingJournals.CountAsync());
    }

    private static async Task ActivateBookAsync(AccountingSetupService setup, AccountingBookCode bookCode) =>
        await setup.ActivateBookAsync(new AccountingBookActivation(
            bookCode,
            new DateOnly(2026, 9, 1),
            "Opening balance is explicitly documented as zero.",
            Actor()));

    private static AccountingPostRequest BalancedCashOpening(
        AccountingBookCode bookCode,
        string requestToken,
        string requestFingerprint) => new(
            bookCode,
            "opening",
            "บันทึกยอดยกมาที่ตรวจสอบแล้ว",
            new DateOnly(2026, 9, 12),
            requestToken,
            requestFingerprint,
            Actor(),
            [
                new("1000", 100, 0),
                new("3000", 0, 100)
            ]);

    private static AccountingPostRequest BalancedReceipt(
        string requestToken,
        string requestFingerprint) => new(
            AccountingBookCode.Welfare,
            "advance_receipt",
            "รับเงินสงเคราะห์ล่วงหน้าสมาชิก",
            new DateOnly(2026, 9, 12),
            requestToken,
            requestFingerprint,
            Actor(),
            [
                new("1000", 100, 0),
                new("2000", 0, 100)
            ]);

    private static AccountingActor Actor() => new("local-user", "ผู้ทดสอบ", "TEST-PC", "test");
}
