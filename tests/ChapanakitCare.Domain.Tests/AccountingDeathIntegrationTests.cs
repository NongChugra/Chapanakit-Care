using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.Deaths;
using Microsoft.EntityFrameworkCore;
using ChapanakitCare.Infrastructure.Accounting;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingDeathIntegrationTests
{
    [Fact]
    public async Task Unfunded_contributors_create_shortfall_and_cannot_be_paid_as_if_cash_was_received()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await store.AddMemberAsync("00001");
        var funded = await store.AddMemberAsync("00002");
        var owing1 = await store.AddMemberAsync("00003");
        var owing2 = await store.AddMemberAsync("00004");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ศูนย์", "open-w", []), FinanceTestStore.Now, "tester");
        await finance.OpenAsync(new("association", FinanceTestStore.Date, "ศูนย์", "open-a", []), FinanceTestStore.Now, "tester");
        await finance.ReceiveAsync(new(FinanceTestStore.Date, "1000", "เงินที่ได้รับจริง", "received", [new(funded.Id, 900)]), FinanceTestStore.Now, "tester");
        var death = await new DeathApplicationService(store.Db).ConfirmAsync(new("00001", "DC", FinanceTestStore.Date, "เหตุ", false, null,
            new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        Assert.Equal(1800, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1200"));
        Assert.Equal(2600, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
        Assert.Equal(900, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => finance.PayBenefitAsync(new(death.DeathCase.Id, 1,
            FinanceTestStore.Date, "1000", 1300, "เงินยังไม่พอ", "too-soon"), FinanceTestStore.Now, "tester"));
        await finance.ReceiveAsync(new(FinanceTestStore.Date, "1000", "ชำระยอดค้าง", "settled", [new(owing1.Id, 900), new(owing2.Id, 900)]), FinanceTestStore.Now, "tester");
        for (var slot = 1; slot <= 2; slot++)
            await finance.PayBenefitAsync(new(death.DeathCase.Id, slot, FinanceTestStore.Date, "1000", 1300, "ลงนามรับแล้ว", $"paid-{slot}"), FinanceTestStore.Now, "tester");
        await finance.RemitFeeAsync(new(FinanceTestStore.Date, "1000", "1000", 100, "นำส่งค่าหัก", "remit"), FinanceTestStore.Now, "tester");
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1200"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
    }

    [Fact]
    public async Task Nonpay_death_creates_no_fee_or_benefit_but_keeps_actual_advance_until_refunded()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "เงินจริง", "open", [new("1000", 901, 0), new("2000", 0, 901, member.Id)]), FinanceTestStore.Now, "tester");
        await new DeathApplicationService(store.Db).ConfirmAsync(new("00001", "DC", FinanceTestStore.Date, "เหตุ", true, "ไม่เข้าเกณฑ์จ่าย",
            new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        Assert.Single(await store.Db.AccountingJournals.ToListAsync());
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
        await finance.RefundAsync(new(member.Id, FinanceTestStore.Date, "1000", 901, "หลักฐานผู้รับเงินจริง", "refund"), FinanceTestStore.Now, "tester");
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
    }

    [Fact]
    public async Task Closed_association_period_rolls_back_death_snapshot_and_both_books()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        for (var index = 1; index <= 4; index++) await store.AddMemberAsync($"{index:00000}");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ศูนย์", "open-w", []), FinanceTestStore.Now, "tester");
        await finance.OpenAsync(new("association", FinanceTestStore.Date, "ศูนย์", "open-a", []), FinanceTestStore.Now, "tester");
        await new AccountingSetupService(store.Db).ClosePeriodAsync(new(AccountingBookCode.Association, FinanceTestStore.Date, FinanceTestStore.Date,
            "ปิดแล้ว", new("tester", "tester", "test", "test")));
        var ledgerCount = await store.Db.AdvanceLedgerEntries.CountAsync();
        await Assert.ThrowsAsync<AccountingPeriodClosedException>(() => new DeathApplicationService(store.Db).ConfirmAsync(new("00001", "DC", FinanceTestStore.Date, "เหตุ", false, null,
            new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester"));
        await store.Db.SaveChangesAsync();
        Assert.Empty(await store.Db.DeathCases.ToListAsync());
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
        Assert.Equal(ledgerCount, await store.Db.AdvanceLedgerEntries.CountAsync());
        Assert.All(await store.Db.Members.ToListAsync(), x => Assert.Equal(MemberStatus.Normal, x.Status));
    }

    [Fact]
    public async Task Active_death_uses_actual_prepaid_and_accrues_fee_in_separate_book_without_paying_cash()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var deceased = await store.AddMemberAsync("00001");
        var first = await store.AddMemberAsync("00002");
        var second = await store.AddMemberAsync("00003");
        var third = await store.AddMemberAsync("00004");
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open-w", []), FinanceTestStore.Now, "tester");
        await finance.OpenAsync(new("association", FinanceTestStore.Date, "เริ่มศูนย์", "open-a", []), FinanceTestStore.Now, "tester");
        await finance.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงินสมาชิก", "receive",
            [new(deceased.Id, 451), new(first.Id, 27000), new(second.Id, 27000), new(third.Id, 27000)]), FinanceTestStore.Now, "tester");
        var deaths = new DeathApplicationService(store.Db);
        var preview = await deaths.PreviewAsync("00001", false, FinanceTestStore.Date);
        Assert.Equal(3051, preview.Calculation.TotalBenefitSatang);
        var result = await deaths.ConfirmAsync(new("00001", "DC-1", FinanceTestStore.Date, "ชรา", false, null,
            new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        Assert.Equal(3051, result.Calculation.TotalBenefitSatang);
        Assert.Equal(81451, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Equal(78300, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
        Assert.Equal(3051, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2200"));
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1300"));
        Assert.Equal(100, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        var payable = await store.Db.AccountingAccounts.SingleAsync(x => x.Role == AccountingAccountRole.WelfareBenefitPayable);
        Assert.Equal(new long[] { 1526, 1525 }, await store.Db.AccountingJournalLines.Where(x => x.AccountId == payable.Id)
            .OrderBy(x => x.BeneficiarySlotNo).Select(x => x.CreditSatang).ToArrayAsync());
    }
}
