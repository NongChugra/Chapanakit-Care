using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class WelfareCollectionTests
{
    [Fact]
    public async Task Collection_requests_preserve_member_group_and_do_not_reset_paid_units()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001", "07");
        member.AdvanceUnitsBalance = -2;
        await store.Db.SaveChangesAsync();
        var service = new WelfareCollectionService(store.Db);
        var rows = await service.CreateAsync(Command("one", member.Id), FinanceTestStore.Now, "tester");

        Assert.Single(rows);
        Assert.Equal(9000, rows[0].AmountSatang);
        Assert.Equal("07", rows[0].GroupNo);
        Assert.Equal(-2, member.AdvanceUnitsBalance);
        member.GroupNo = "08";
        await store.Db.SaveChangesAsync();
        Assert.Equal("07", (await store.Db.Set<WelfareCollection>().AsNoTracking().SingleAsync()).GroupNo);
    }

    [Fact]
    public async Task Identical_request_replays_but_changed_payload_and_duplicate_cycle_are_rejected()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new WelfareCollectionService(store.Db);
        var command = Command("one", member.Id);
        var first = await service.CreateAsync(command, FinanceTestStore.Now, "tester");
        var again = await service.CreateAsync(command, FinanceTestStore.Now.AddMinutes(1), "tester");
        Assert.Equal(first[0].Id, again[0].Id);
        await Assert.ThrowsAsync<MemberValidationException>(() => service.CreateAsync(
            command with { Lines = [new(member.Id, 18000)] }, FinanceTestStore.Now, "tester"));
        await Assert.ThrowsAsync<MemberValidationException>(() => service.CreateAsync(
            command with { RequestToken = "two" }, FinanceTestStore.Now, "tester"));
        Assert.Single(await store.Db.Set<WelfareCollection>().ToListAsync());
    }

    [Fact]
    public async Task Invalid_member_in_batch_leaves_no_partial_collections()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new WelfareCollectionService(store.Db);
        await Assert.ThrowsAsync<MemberValidationException>(() => service.CreateAsync(
            Command("one", member.Id) with { Lines = [new(member.Id, 9000), new(Guid.NewGuid(), 9000)] },
            FinanceTestStore.Now, "tester"));
        Assert.Empty(await store.Db.Set<WelfareCollection>().ToListAsync());
    }

    private static CreateWelfareCollection Command(string token, Guid memberId) => new(
        "2569-09", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(30),
        "เรียกเก็บเงินสงเคราะห์ล่วงหน้า", token, [new(memberId, 9000)]);
}
