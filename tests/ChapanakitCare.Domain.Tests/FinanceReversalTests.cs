using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class FinanceReversalTests
{
    [Fact]
    public async Task Receipt_reversal_appends_opposite_entries_and_restores_member_projection_once()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var receipt = await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงิน", "receipt", [new(member.Id, 1000)]), FinanceTestStore.Now, "tester");
        var reversal = new ReverseFinanceDocument(receipt, FinanceTestStore.Date, "บันทึกซ้ำ ตรวจหลักฐานและคืนเงินแล้ว", "reverse");
        var id = await service.ReverseAsync(reversal, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await service.ReverseAsync(reversal, FinanceTestStore.Now, "tester"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(0, (await store.Db.Members.AsNoTracking().SingleAsync()).AdvanceUnitsBalance);
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync());
        Assert.Equal(receipt, (await store.Db.AccountingJournals.SingleAsync(x => x.Id == id)).ReversesJournalId);
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.ReverseAsync(reversal with { RequestToken = "duplicate" }, FinanceTestStore.Now, "tester"));
    }

    [Fact]
    public async Task Reversal_rejects_receipt_with_later_financial_activity()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var receipt = await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงิน", "receipt", [new(member.Id, 1000)]), FinanceTestStore.Now, "tester");
        await service.TransferAsync(new("welfare", FinanceTestStore.Date, "1000", "1100", 1000, "นำฝากแล้ว", "transfer"), FinanceTestStore.Now, "tester");
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.ReverseAsync(new(receipt, FinanceTestStore.Date, "ย้อนรายการเดิม", "reverse"), FinanceTestStore.Now, "tester"));
        Assert.Equal(1000, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1100"));
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync());
    }

    [Fact]
    public async Task Fee_remittance_reversal_restores_both_books_atomically()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดนำส่ง", "open-w", [new("1000", 100, 0), new("2200", 0, 100)]), FinanceTestStore.Now, "tester");
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ยอดค้างรับ", "open-a", [new("1300", 100, 0), new("3000", 0, 100)]), FinanceTestStore.Now, "tester");
        var remit = await service.RemitFeeAsync(new(FinanceTestStore.Date, "1000", "1000", 60, "นำส่ง", "remit"), FinanceTestStore.Now, "tester");
        await service.ReverseAsync(new(remit, FinanceTestStore.Date, "คืนเงินนำส่งแล้วตามหลักฐาน", "reverse"), FinanceTestStore.Now, "tester");
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2200"));
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1300"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync(x => x.ReversesJournalId != null));
    }
}
