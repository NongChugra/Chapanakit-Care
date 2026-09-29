using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Web.Pages.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.Deaths;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingOpeningPageTests
{
    [Fact]
    public async Task Guided_opening_preserves_existing_beneficiary_debt_and_additional_bank()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await store.AddMemberAsync("00001");
        var death = await new DeathApplicationService(store.Db).ConfirmAsync(new("00001", "DC", FinanceTestStore.Date, "เหตุ", false, null,
            new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        var setup = new AccountingSetupService(store.Db);
        await setup.EnsureCatalogAsync();
        await setup.AddBankAccountAsync(new(AccountingBookCode.Welfare, "1101", "ธนาคารสมาชิก", "123", new("tester", "tester", "test", "test")));
        var page = new OpeningModel(store.Db)
        {
            Book = "welfare", BusinessDate = "12/09/2569", Evidence = "ยอดธนาคารและภาระเก่า", RequestToken = "opening",
            AdditionalMoney = [new() { AccountCode = "1101", Amount = "10.01" }],
            BenefitBalances = [new() { DeathCaseId = death.DeathCase.Id, BeneficiarySlot = 1, Amount = "10.01" }]
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(1001, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1101"));
        Assert.Equal(1001, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
        Assert.Single(await store.Db.AccountingJournals.ToListAsync());
        Assert.Single(await store.Db.DeathCases.ToListAsync());
    }

    [Fact]
    public async Task Guided_opening_records_cash_and_individual_advance_from_explicit_evidence()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var page = new OpeningModel(store.Db)
        {
            Book = "welfare", BusinessDate = "12/09/2569", Cash = "10.00", Evidence = "สมุดเงินสดและทะเบียนเงินล่วงหน้า", RequestToken = "opening",
            MemberBalances = [new() { MemberId = member.Id, Advance = "10.00" }]
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(1000, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(1000, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
    }

    [Fact]
    public async Task Guided_opening_does_not_invent_accumulated_fund_to_balance_cash()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var page = new OpeningModel(store.Db)
        { Book = "association", BusinessDate = "12/09/2569", Cash = "10.00", Evidence = "ยังไม่ครบหลักฐาน", RequestToken = "opening" };
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.False(page.ModelState.IsValid);
        Assert.False(await store.Db.AccountingBooks.AnyAsync(x => x.IsActivated));
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }

    [Fact]
    public async Task Empty_opening_requires_explicit_zero_confirmation()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var page = new OpeningModel(store.Db)
        { Book = "association", BusinessDate = "12/09/2569", Evidence = "ตรวจยอดเป็นศูนย์", RequestToken = "opening" };
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.False(await store.Db.AccountingBooks.AnyAsync(x => x.IsActivated));
        page.ModelState.Clear();
        page.ZeroConfirmed = true;
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.True(await store.Db.AccountingBooks.AnyAsync(x => x.IsActivated && x.Code == AccountingBookCode.Association));
    }
}
