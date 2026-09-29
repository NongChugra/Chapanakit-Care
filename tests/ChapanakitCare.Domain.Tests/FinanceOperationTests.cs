using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class FinanceOperationTests
{
    [Fact]
    public async Task Welfare_opening_projects_verified_member_money_and_does_not_keep_legacy_paid_units()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var funded = await store.AddMemberAsync("00001");
        var unpaid = await store.AddMemberAsync("00002");
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดยกมาตามหลักฐาน", "opening",
            [new("1000", 1000, 0), new("2000", 0, 1000, funded.Id)]), FinanceTestStore.Now, "tester");
        Assert.Equal(1, (await store.Db.Members.AsNoTracking().SingleAsync(x => x.Id == funded.Id)).AdvanceUnitsBalance);
        Assert.Equal(0, (await store.Db.Members.AsNoTracking().SingleAsync(x => x.Id == unpaid.Id)).AdvanceUnitsBalance);
        Assert.Equal(1000, await Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(2, await store.Db.AdvanceLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Opening_records_supplied_money_in_separate_books_without_creating_income()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(Opening(member.Id), FinanceTestStore.Now, "tester");
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ตรวจสอบบัญชีเริ่มต้นเป็นศูนย์", "open-association", []), FinanceTestStore.Now, "tester");

        Assert.Equal(27000, await Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(27000, await Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(0, await Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(0, await Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal(2, await store.Db.AccountingBooks.CountAsync(x => x.IsActivated));
    }

    [Fact]
    public async Task Receipt_adds_real_member_advance_and_replay_cannot_add_money_twice()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(Opening(member.Id), FinanceTestStore.Now, "tester");
        var command = new ReceiveWelfareMoney(FinanceTestStore.Date, "1000", "รับเงินล่วงหน้า", "receive-one", [new(member.Id, 451)]);
        var first = await service.ReceiveAsync(command, FinanceTestStore.Now, "tester");
        var second = await service.ReceiveAsync(command, FinanceTestStore.Now, "tester");

        Assert.Equal(first, second);
        Assert.Equal(27451, await Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(27451, await Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(0, await Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal(30, member.AdvanceUnitsBalance);
    }

    [Fact]
    public async Task Receipt_clears_recorded_shortfall_before_adding_advance()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดลูกหนี้และทุนตามเอกสาร", "open",
            [new("1200", 449, 0, member.Id), new("3000", 0, 449)]), FinanceTestStore.Now, "tester");
        await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "ชำระยอดค้างและเติมเงิน", "receive", [new(member.Id, 1000)]), FinanceTestStore.Now, "tester");

        Assert.Equal(1000, await Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(0, await Balance(store, AccountingBookCode.Welfare, "1200"));
        Assert.Equal(551, await Balance(store, AccountingBookCode.Welfare, "2000"));
    }

    [Fact]
    public async Task Opening_income_and_unsupported_member_dimensions_are_rejected_without_activation()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.OpenAsync(
            new("association", FinanceTestStore.Date, "ผิดประเภท", "bad", [new("1000", 100, 0), new("4000", 0, 100)]), FinanceTestStore.Now, "tester"));
        Assert.False(await store.Db.AccountingBooks.AnyAsync(x => x.IsActivated));
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.OpenAsync(
            new("welfare", FinanceTestStore.Date, "ไม่มีรายละเอียดสมาชิก", "bad2", [new("1000", 100, 0), new("2000", 0, 100)]), FinanceTestStore.Now, "tester"));
        Assert.False(await store.Db.AccountingBooks.AnyAsync(x => x.IsActivated));
    }

    [Fact]
    public async Task Collection_receipt_records_partial_payment_and_rejects_overpayment_without_side_effects()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        await new WelfareCollectionService(store.Db).CreateAsync(new("2026-09", FinanceTestStore.Date,
            FinanceTestStore.Date.AddDays(30), "เรียกเก็บ", "collection", [new(member.Id, 1000)]), FinanceTestStore.Now, "tester");
        var collection = await store.Db.Set<WelfareCollection>().SingleAsync();
        await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับบางส่วน", "partial", [new(member.Id, 400, collection.Id)]), FinanceTestStore.Now, "tester");
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.ReceiveAsync(new(FinanceTestStore.Date,
            "1000", "เกินยอดเรียกเก็บ", "too-much", [new(member.Id, 601, collection.Id)]), FinanceTestStore.Now, "tester"));
        Assert.Equal(400, await Balance(store, AccountingBookCode.Welfare, "1000"));
        var allocated = await store.Db.AccountingJournalLines.Where(x => x.CollectionRequestId == collection.Id).ToListAsync();
        Assert.Equal(400, allocated.Sum(x => x.CreditSatang - x.DebitSatang));
        await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "ส่วนที่เหลือ", "rest", [new(member.Id, 600, collection.Id)]), FinanceTestStore.Now, "tester");
        Assert.Equal(1000, await Balance(store, AccountingBookCode.Welfare, "2000"));
    }

    [Fact]
    public async Task Failed_group_receipt_cannot_leak_counter_changes_into_a_later_save()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var before = member.AdvanceUnitsBalance;
        var ledgerCount = await store.Db.AdvanceLedgerEntries.CountAsync();
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.ReceiveAsync(new(FinanceTestStore.Date,
            "1000", "สมาชิกในชุดไม่ถูกต้อง", "bad-group", [new(member.Id, 900), new(Guid.NewGuid(), 900)]), FinanceTestStore.Now, "tester"));
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();
        Assert.Equal(before, (await store.Db.Members.SingleAsync()).AdvanceUnitsBalance);
        Assert.Equal(ledgerCount, await store.Db.AdvanceLedgerEntries.CountAsync());
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }

    private static OpenFinanceBook Opening(Guid memberId) => new("welfare", FinanceTestStore.Date,
        "เงินสดและเงินล่วงหน้าตามเอกสารที่ตรวจสอบ", "open-welfare", [new("1000", 27000, 0), new("2000", 0, 27000, memberId)]);

    internal static async Task<long> Balance(FinanceTestStore store, AccountingBookCode book, string code)
    {
        var account = await (from a in store.Db.AccountingAccounts join b in store.Db.AccountingBooks on a.BookId equals b.Id
            where b.Code == book && a.Code == code select a).SingleAsync();
        var lines = await store.Db.AccountingJournalLines.Where(x => x.AccountId == account.Id).ToListAsync();
        var debitBalance = lines.Sum(x => x.DebitSatang - x.CreditSatang);
        return account.NormalBalance == AccountingNormalBalance.Debit ? debitBalance : -debitBalance;
    }
}
