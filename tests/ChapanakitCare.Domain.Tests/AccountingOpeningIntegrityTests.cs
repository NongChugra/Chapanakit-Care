using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingOpeningIntegrityTests
{
    [Fact]
    public async Task Failed_opening_rolls_back_activation_and_allows_a_corrected_request_in_the_same_context()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        var tooManyUnits = checked(((long)int.MaxValue + 1) * 900);
        await Assert.ThrowsAsync<OverflowException>(() => service.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดผิด", "bad",
            [new("1000", tooManyUnits, 0), new("2000", 0, tooManyUnits, member.Id)]), FinanceTestStore.Now, "tester"));
        Assert.False(await store.Db.AccountingBooks.AsNoTracking().AnyAsync(x => x.IsActivated));
        Assert.Empty(await store.Db.AccountingJournals.AsNoTracking().ToListAsync());
        Assert.Equal(Guid.Empty, await service.OpenAsync(new("welfare", FinanceTestStore.Date, "แก้ไขเป็นศูนย์", "correct", []), FinanceTestStore.Now, "tester"));
        Assert.True(await store.Db.AccountingBooks.AsNoTracking().AnyAsync(x => x.IsActivated));
    }

    [Fact]
    public async Task Zero_opening_request_replays_after_later_receipt_and_rejects_changed_token_payload()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        var opening = new OpenFinanceBook("welfare", FinanceTestStore.Date, "ตรวจแล้วเป็นศูนย์", "open-zero", []);
        Assert.Equal(Guid.Empty, await service.OpenAsync(opening, FinanceTestStore.Now, "tester"));
        await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงินจริง", "receipt", [new(member.Id, 901)]), FinanceTestStore.Now, "tester");
        Assert.Equal(Guid.Empty, await service.OpenAsync(opening, FinanceTestStore.Now, "tester"));
        await Assert.ThrowsAsync<AccountingIdempotencyConflictException>(() => service.OpenAsync(opening with { Evidence = "เปลี่ยนหลักฐาน" }, FinanceTestStore.Now, "tester"));
        await Assert.ThrowsAsync<AccountingIdempotencyConflictException>(() => service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "ใช้คำขอซ้ำ", "open-zero", [new(member.Id, 100)]), FinanceTestStore.Now, "tester"));
        Assert.Single(await store.Db.AccountingJournals.ToListAsync());
    }
}
