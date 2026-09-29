using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingAdjustmentTests
{
    [Fact]
    public async Task Reclassification_form_posts_the_selected_document_and_categories()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("association", FinanceTestStore.Date, "เงินทุน", "open", [new("1000", 100, 0), new("3000", 0, 100)]), FinanceTestStore.Now, "tester");
        await new AccountingSetupService(store.Db).AddExpenseCategoryAsync(new("5001", "วัสดุ", new("tester", "tester", "test", "test")));
        var original = await finance.PayExpenseAsync(new(FinanceTestStore.Date, "1000", "5000", 100, "ร้านค้า", "จ่ายแล้ว", "expense"), FinanceTestStore.Now, "tester");
        var page = new ChapanakitCare.Web.Pages.Accounting.TransactionModel(store.Db)
        { Kind = "reclassify", OriginalJournalId = original, ExpenseAccount = "5000", TargetAccount = "5001", BusinessDate = "12/09/2569", Amount = "1.00", Evidence = "แก้หมวดตามหลักฐาน", RequestToken = "form" };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "5001"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
    }

    [Fact]
    public async Task Expense_reclassification_corrects_an_older_document_without_changing_cash_or_total_expense()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("association", FinanceTestStore.Date, "เงินจริง", "open", [new("1000", 20000, 0), new("3000", 0, 20000)]), FinanceTestStore.Now, "tester");
        await new AccountingSetupService(store.Db).AddExpenseCategoryAsync(new("5001", "วัสดุสำนักงาน", new("tester", "tester", "test", "test")));
        var original = await finance.PayExpenseAsync(new(FinanceTestStore.Date, "1000", "5000", 10000, "ร้านค้า", "หลักฐานจ่ายเดิม", "expense"), FinanceTestStore.Now, "tester");
        await finance.TransferAsync(new("association", FinanceTestStore.Date, "1000", "1100", 5000, "นำฝากภายหลัง", "deposit"), FinanceTestStore.Now, "tester");
        var command = new ReclassifyExpense(original, FinanceTestStore.Date, "5000", "5001", 6000, "ตรวจพบส่วนที่เป็นวัสดุ", "adjust");
        var correction = await finance.ReclassifyExpenseAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(correction, await finance.ReclassifyExpenseAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(5000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(5000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1100"));
        Assert.Equal(4000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "5000"));
        Assert.Equal(6000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "5001"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal("หลักฐานจ่ายเดิม", (await store.Db.AccountingJournals.SingleAsync(x => x.Id == original)).DescriptionSnapshot);
        await Assert.ThrowsAsync<AccountingValidationException>(() => finance.ReclassifyExpenseAsync(command with { AmountSatang = 4001, RequestToken = "over" }, FinanceTestStore.Now, "tester"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => finance.ReclassifyExpenseAsync(command with { ToExpenseAccount = "4000", RequestToken = "income" }, FinanceTestStore.Now, "tester"));
        Assert.Equal(4, await store.Db.AccountingJournals.CountAsync());
    }
}
