using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingResignationTests
{
    [Fact]
    public async Task Resigned_member_can_settle_debt_without_reopening_coverage_or_prepaying()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดค้างจริง", "open", [new("1200", 901, 0, member.Id), new("3000", 0, 901)]), FinanceTestStore.Now, "tester");
        await new ResignationApplicationService(store.Db).ConfirmAsync("00001", FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        await Assert.ThrowsAsync<AccountingValidationException>(() => finance.ReceiveAsync(new(FinanceTestStore.Date, "1000", "เกินหนี้", "over", [new(member.Id, 902)]), FinanceTestStore.Now, "tester"));
        var command = new ReceiveWelfareMoney(FinanceTestStore.Date, "1000", "ชำระหนี้", "settle", [new(member.Id, 900)]);
        var id = await finance.ReceiveAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await finance.ReceiveAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(1, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1200"));
        Assert.Equal(0, member.AdvanceUnitsBalance);
        Assert.Equal(MemberStatus.Resigned, member.Status);
        await finance.ReverseAsync(new(id, FinanceTestStore.Date, "ยกเลิกรับผิด", "reverse"), FinanceTestStore.Now, "tester");
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1200"));
        Assert.Equal(0, member.AdvanceUnitsBalance);
    }

    [Fact]
    public async Task Refund_form_records_actual_payment_and_refund_reversal_preserves_resigned_status()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดจริง", "open", [new("1000", 901, 0), new("2000", 0, 901, member.Id)]), FinanceTestStore.Now, "tester");
        await new ResignationApplicationService(store.Db).ConfirmAsync("00001", FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        var page = new ChapanakitCare.Web.Pages.Accounting.TransactionModel(store.Db)
        { Kind = "refund", MemberId = member.Id, BusinessDate = "12/09/2569", MoneyAccount = "1000", Amount = "9.01", Evidence = "ผู้รับลงนาม", RequestToken = "refund-form" };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(await page.OnPostAsync());
        var journal = await store.Db.AccountingJournals.SingleAsync(x => x.RequestToken == "refund-form");
        await finance.ReverseAsync(new(journal.Id, FinanceTestStore.Date, "ยกเลิกจ่ายผิด", "reverse"), FinanceTestStore.Now, "tester");
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(0, member.AdvanceUnitsBalance);
        Assert.Equal(MemberStatus.Resigned, member.Status);
    }

    [Fact]
    public async Task Resignation_uses_exact_money_and_keeps_it_payable_until_actual_refund()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเงินจริง", "open", [new("1000", 901, 0), new("2000", 0, 901, member.Id)]), FinanceTestStore.Now, "tester");
        var resignations = new ResignationApplicationService(store.Db);
        Assert.Equal(901, (await resignations.PreviewAsync("00001"))!.RefundSatang);
        var result = await resignations.ConfirmAsync("00001", FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        Assert.Equal(901, result.RefundSatang);
        Assert.Equal(MemberStatus.Resigned, result.Member.Status);
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Single(await store.Db.AccountingJournals.ToListAsync());
    }

    [Fact]
    public async Task Actual_refund_reduces_resigned_member_liability_once_and_rejects_overpayment()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเงินจริง", "open", [new("1000", 901, 0), new("2000", 0, 901, member.Id)]), FinanceTestStore.Now, "tester");
        await new ResignationApplicationService(store.Db).ConfirmAsync("00001", FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        var command = new RefundMemberAdvance(member.Id, FinanceTestStore.Date, "1000", 900, "ผู้รับลงนามรับเงินแล้ว", "refund");
        var id = await finance.RefundAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await finance.RefundAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(1, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(1, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => finance.RefundAsync(command with { RequestToken = "over", AmountSatang = 2 }, FinanceTestStore.Now, "tester"));
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync());
    }
}
