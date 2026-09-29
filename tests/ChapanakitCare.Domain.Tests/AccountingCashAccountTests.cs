using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingCashAccountTests
{
    [Fact]
    public async Task Management_form_adds_a_named_cash_holder_in_selected_book()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var page = new ChapanakitCare.Web.Pages.Accounting.ManageModel(store.Db)
        { Book = "welfare", Action = "cash", AccountCode = "1001", AccountName = "ผู้ประสานงานกลุ่ม 01" };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(AccountingAccountRole.Cash, (await store.Db.AccountingAccounts.SingleAsync(x => x.Code == "1001")).Role);
    }

    [Fact]
    public async Task Collector_cash_is_separate_and_depositing_it_is_a_transfer_without_second_receipt()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var setup = new AccountingSetupService(store.Db);
        await setup.EnsureCatalogAsync();
        var actor = new AccountingActor("tester", "tester", "test", "test");
        var cash = await setup.AddCashAccountAsync(new(AccountingBookCode.Welfare, "1001", "เงินสดผู้ประสานงานกลุ่ม 01", actor));
        Assert.Equal(AccountingAccountRole.Cash, cash.Role);
        Assert.False(cash.IsBankAccount);
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        await service.ReceiveAsync(new(FinanceTestStore.Date, "1001", "รับโดยผู้ประสานงาน", "receipt", [new(member.Id, 1000)]), FinanceTestStore.Now, "tester");
        await service.TransferAsync(new("welfare", FinanceTestStore.Date, "1001", "1100", 600, "นำฝากพร้อมใบนำฝาก", "deposit"), FinanceTestStore.Now, "tester");
        Assert.Equal(400, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1001"));
        Assert.Equal(600, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1100"));
        Assert.Equal(1000, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync());
        await Assert.ThrowsAsync<AccountingValidationException>(() => setup.AddCashAccountAsync(new(AccountingBookCode.Welfare, "4001", "ห้ามรายได้ใหม่", actor)));
        await Assert.ThrowsAsync<AccountingValidationException>(() => setup.AddCashAccountAsync(new(AccountingBookCode.Welfare, "1001", "ห้ามซ้ำ", actor)));
    }
}
