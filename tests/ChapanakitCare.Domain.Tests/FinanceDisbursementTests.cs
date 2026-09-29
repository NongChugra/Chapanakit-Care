using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;
using ChapanakitCare.Infrastructure.Deaths;

namespace ChapanakitCare.Domain.Tests;

public sealed class FinanceDisbursementTests
{
    [Fact]
    public async Task Benefit_payment_cannot_exceed_the_selected_beneficiary_opening_payable()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await store.AddMemberAsync("00001");
        var death = await new DeathApplicationService(store.Db).ConfirmAsync(new("00001", "DC-1", FinanceTestStore.Date,
            "ชรา", false, null, new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())),
            FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดค้างจ่ายก่อนเริ่มบัญชี", "open",
            [new("1000", 10000, 0), new("2100", 0, 10000, DeathCaseId: death.DeathCase.Id, BeneficiarySlot: 1)]), FinanceTestStore.Now, "tester");
        var command = new PayWelfareBenefit(death.DeathCase.Id, 1, FinanceTestStore.Date, "1000", 6000, "หลักฐานจ่าย", "pay");
        var id = await service.PayBenefitAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await service.PayBenefitAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(4000, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
        Assert.Equal(4000, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.PayBenefitAsync(command with { AmountSatang = 4001, RequestToken = "over" }, FinanceTestStore.Now, "tester"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.PayBenefitAsync(command with { BeneficiarySlot = 2, AmountSatang = 1, RequestToken = "other" }, FinanceTestStore.Now, "tester"));
    }

    [Fact]
    public async Task Remittance_settles_reciprocal_balances_without_recognizing_income_again()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดค่าหักค้างนำส่ง", "open-welfare",
            [new("1000", 100, 0), new("2200", 0, 100)]), FinanceTestStore.Now, "tester");
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ยอดค่าหักค้างรับ", "open-association",
            [new("1300", 100, 0), new("3000", 0, 100)]), FinanceTestStore.Now, "tester");
        var command = new RemitAssociationFee(FinanceTestStore.Date, "1000", "1000", 60, "หลักฐานนำส่ง", "remit");
        var id = await service.RemitFeeAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await service.RemitFeeAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(40, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2200"));
        Assert.Equal(40, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1300"));
        Assert.Equal(60, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal(4, await store.Db.AccountingJournals.CountAsync());
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.RemitFeeAsync(command with { AmountSatang = 41, RequestToken = "over" }, FinanceTestStore.Now, "tester"));
    }

    [Fact]
    public async Task Operating_expense_reduces_association_cash_and_replay_does_not_spend_twice()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ยอดเงินตามหลักฐาน", "open",
            [new("1000", 10000, 0), new("3000", 0, 10000)]), FinanceTestStore.Now, "tester");
        var command = new PayOperatingExpense(FinanceTestStore.Date, "1000", "5000", 6000, "ร้านเครื่องเขียน", "ใบสำคัญ 001", "expense");
        var id = await service.PayExpenseAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await service.PayExpenseAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(4000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(6000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "5000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.PayExpenseAsync(command with { AmountSatang = 4001, RequestToken = "overdraw" }, FinanceTestStore.Now, "tester"));
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync());
    }

    [Fact]
    public async Task Transfer_moves_cash_to_bank_in_same_book_without_income_or_expense()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ยอดเงินตามหลักฐาน", "open",
            [new("1000", 10000, 0), new("3000", 0, 10000)]), FinanceTestStore.Now, "tester");
        var command = new TransferBookMoney("association", FinanceTestStore.Date, "1000", "1100", 7500, "ใบนำฝาก", "transfer");
        var id = await service.TransferAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(id, await service.TransferAsync(command, FinanceTestStore.Now, "tester"));
        Assert.Equal(2500, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(7500, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1100"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "5000"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.TransferAsync(command with { ToAccount = "1000", RequestToken = "same" }, FinanceTestStore.Now, "tester"));
        await Assert.ThrowsAsync<AccountingValidationException>(() => service.TransferAsync(command with { AmountSatang = 2501, RequestToken = "overdraw" }, FinanceTestStore.Now, "tester"));
    }
}
