using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingReconciliationTests
{
    [Fact]
    public async Task Reconciliation_freezes_statement_difference_without_creating_an_adjustment()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ยอดตามเอกสาร", "open",
            [new("1000", 10000, 0), new("3000", 0, 10000)]), FinanceTestStore.Now, "tester");
        var command = new ReconcileMoneyAccount("association", "1000", FinanceTestStore.Date, 9950, "ตรวจนับเงินจริง", "reconcile");
        var first = await service.ReconcileAsync(command, FinanceTestStore.Now, "tester");
        Assert.Equal(10000, first.BookBalanceSatang);
        Assert.Equal(-50, first.DifferenceSatang);
        Assert.Equal(first.Id, (await service.ReconcileAsync(command, FinanceTestStore.Now, "tester")).Id);
        Assert.Single(await store.Db.AccountingJournals.ToListAsync());
        Assert.Single(await store.Db.AuditEvents.Where(x => x.Action == "accounting.reconciled").ToListAsync());
        await Assert.ThrowsAsync<AccountingIdempotencyConflictException>(() => service.ReconcileAsync(command with { StatementBalanceSatang = 9900 }, FinanceTestStore.Now, "tester"));
        await service.PayExpenseAsync(new(FinanceTestStore.Date.AddDays(1), "1000", "5000", 1000, "ผู้รับเงิน", "ใบสำคัญ", "expense"), FinanceTestStore.Now, "tester");
        var historical = await service.ReconcileAsync(command with { RequestToken = "recheck" }, FinanceTestStore.Now, "tester");
        Assert.Equal(10000, historical.BookBalanceSatang);
        Assert.Equal(9000, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
    }
}
