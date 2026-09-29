using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Web.Pages.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingManagementPageTests
{
    [Fact]
    public async Task Management_form_adds_book_specific_bank_and_association_expense_without_extra_income()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var bank = new ManageModel(store.Db) { Book = "welfare", Action = "bank", AccountCode = "1101", AccountName = "ธนาคารสมาชิก", BankDescription = "บัญชี 123" };
        Assert.IsType<RedirectToPageResult>(await bank.OnPostAsync());
        var account = await store.Db.AccountingAccounts.SingleAsync(x => x.Code == "1101");
        Assert.Equal(AccountingBookCode.Welfare, (await store.Db.AccountingBooks.SingleAsync(x => x.Id == account.BookId)).Code);
        var expense = new ManageModel(store.Db) { Book = "association", Action = "expense", AccountCode = "5001", AccountName = "ค่าวัสดุ" };
        Assert.IsType<RedirectToPageResult>(await expense.OnPostAsync());
        Assert.Equal(AccountingAccountRole.OperatingExpense, (await store.Db.AccountingAccounts.SingleAsync(x => x.Code == "5001")).Role);
        var forbidden = new ManageModel(store.Db) { Book = "association", Action = "income", AccountCode = "4001", AccountName = "รายได้อื่น" };
        Assert.IsType<PageResult>(await forbidden.OnPostAsync());
        Assert.False(forbidden.ModelState.IsValid);
        Assert.Single(await store.Db.AccountingAccounts.Where(x => x.AccountType == AccountingAccountType.Income).ToListAsync());
    }

    [Fact]
    public async Task Management_form_closes_and_reopens_selected_period_with_evidence()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var page = new ManageModel(store.Db) { Book = "welfare", Action = "close", From = "12/09/2569", To = "30/09/2569", Evidence = "ตรวจสอบงวดแล้ว" };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(AccountingPeriodStatus.Closed, (await store.Db.AccountingPeriods.SingleAsync()).Status);
        page.Action = "reopen";
        page.Evidence = "อนุมัติแก้รายการผิด";
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        var period = await store.Db.AccountingPeriods.SingleAsync();
        Assert.Equal(AccountingPeriodStatus.Open, period.Status);
        Assert.Equal("อนุมัติแก้รายการผิด", period.ReopenReason);
        page.Action = "close";
        page.Evidence = "ตรวจรายการแก้ไขและปิดอีกครั้ง";
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(AccountingPeriodStatus.Closed, period.Status);
        var history = await store.Db.AuditEvents.Where(x => x.EntityType == "accounting_period").ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.Contains(history, x => x.Reason == "ตรวจสอบงวดแล้ว");
        Assert.Contains(history, x => x.Reason == "อนุมัติแก้รายการผิด");
        Assert.Contains(history, x => x.Reason == "ตรวจรายการแก้ไขและปิดอีกครั้ง");
    }

    [Fact]
    public async Task Management_form_records_statement_difference_without_adjusting_money()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("association", FinanceTestStore.Date, "เงินยกมา", "open", [new("1100", 10000, 0), new("3000", 0, 10000)]), FinanceTestStore.Now, "tester");
        var page = new ManageModel(store.Db) { Book = "association", Action = "reconcile", AccountCode = "1100", AsOf = "12/09/2569", StatementBalance = "99.50", Evidence = "รายการเดินบัญชี", RequestToken = "statement" };
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.True(page.ModelState.IsValid);
        Assert.NotNull(page.Reconciliation);
        Assert.Equal(-50, page.Reconciliation!.DifferenceSatang);
        Assert.Equal(10000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1100"));
        Assert.Single(await store.Db.AccountingJournals.ToListAsync());
    }
}
