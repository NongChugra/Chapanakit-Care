using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static ChapanakitCare.Domain.Tests.CoordinatorWorkflowTests;

namespace ChapanakitCare.Domain.Tests;

public sealed class CoordinatorIntegrityTests
{
    private static readonly DateOnly Today = new(2026, 9, 4);
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 5, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("group")]
    [InlineData("status")]
    [InlineData("archive")]
    public async Task Tracked_member_mutations_cannot_leave_an_ineligible_current_leader(string change)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("group:A", "appoint", f.First.Id, 0, null);
        if (change == "group") f.First.GroupNo = "B";
        if (change == "status") f.First.Status = MemberStatus.Resigned;
        if (change == "archive") { f.First.ArchivedAtUtc = Now; f.First.ArchiveReason = "archive"; }
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.Members.SingleAsync(x => x.Id == f.First.Id);
        Assert.Equal("A", stored.GroupNo);
        Assert.Equal(MemberStatus.Normal, stored.Status);
        Assert.Null(stored.ArchivedAtUtc);
    }

    [Fact]
    public async Task Member_edit_allows_group_change_after_role_is_ended()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("group:A", "appoint", f.First.Id, 0, null);
        UpdateMemberCommand command = new(f.First.Version, null, "ทดสอบ", "สมาชิก", null, null, null,
            null, null, null, null, null, null, "B", f.First.ApplicationDate, f.First.ApprovalDate, []);
        await Assert.ThrowsAsync<MemberValidationException>(() => new MemberApplicationService(f.Db).UpdateAsync(f.First.Id, command, Now, "tester"));
        f.Db.ChangeTracker.Clear();
        await f.Change("group:A", "end", null, 1, f.First.Id, "ย้ายกลุ่ม");
        await new MemberApplicationService(f.Db).UpdateAsync(f.First.Id, command, Now, "tester");
        Assert.Equal("B", (await f.Db.Members.SingleAsync(x => x.Id == f.First.Id)).GroupNo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Role_history_cannot_be_edited_or_deleted(bool delete)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        var history = await f.Db.CoordinatorEvents.SingleAsync();
        if (delete) f.Db.CoordinatorEvents.Remove(history); else history.MemberName = "edited";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Contains("ทดสอบ", (await f.Db.CoordinatorEvents.SingleAsync()).MemberName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resignation_and_death_end_current_role_in_the_same_transaction(bool death)
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.MemberBeneficiaries.Add(TestData.Beneficiary(1));
        await f.Db.SaveChangesAsync();
        await f.Change("group:A", "appoint", f.First.Id, 0, null);
        if (death)
            await new DeathApplicationService(f.Db).ConfirmAsync(new("00001", "DC-ROLE", Today, "ชรา", false, null,
                new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), Today, Now, "tester");
        else
            await new ResignationApplicationService(f.Db).ConfirmAsync("00001", Today, Now, "tester");
        Assert.Null((await f.Db.CoordinatorPositions.SingleAsync()).MemberId);
        var ended = await f.Db.CoordinatorEvents.SingleAsync(x => x.Action == "end");
        Assert.Equal(Today, ended.EffectiveDate);
        Assert.Equal(f.First.Id, ended.PreviousMemberId);
        Assert.Contains(death ? "เสียชีวิต" : "ลาออก", ended.Reason);
        Assert.Equal(death ? MemberStatus.Deceased : MemberStatus.Resigned, f.First.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_role_history_write_rolls_back_status_change_and_ledger(bool death)
    {
        await using var f = await Fixture.CreateAsync();
        f.First.CoverageStartDate = Today.AddDays(-1);
        f.Db.MemberBeneficiaries.Add(TestData.Beneficiary(1));
        await f.Db.SaveChangesAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_role_end BEFORE INSERT ON coordinator_events WHEN NEW.action = 'end' BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        if (death)
            await Assert.ThrowsAsync<DbUpdateException>(() => new DeathApplicationService(f.Db).ConfirmAsync(new("00001", "DC-FAIL", Today, "ชรา", false, null,
                new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), Today, Now, "tester"));
        else
            await Assert.ThrowsAsync<DbUpdateException>(() => new ResignationApplicationService(f.Db).ConfirmAsync("00001", Today, Now, "tester"));
        f.Db.ChangeTracker.Clear();
        var member = await f.Db.Members.SingleAsync(x => x.Id == f.First.Id);
        Assert.Equal(MemberStatus.Normal, member.Status);
        Assert.Equal(30, member.AdvanceUnitsBalance);
        Assert.Equal(30, (await f.Db.Members.SingleAsync(x => x.Id == f.Second.Id)).AdvanceUnitsBalance);
        Assert.Equal(f.First.Id, (await f.Db.CoordinatorPositions.SingleAsync()).MemberId);
        Assert.Single(await f.Db.CoordinatorEvents.ToListAsync());
        Assert.Empty(await f.Db.MemberStatusEvents.ToListAsync());
        Assert.Empty(await f.Db.AdvanceLedgerEntries.ToListAsync());
        Assert.Empty(await f.Db.DeathCases.ToListAsync());
        Assert.Empty(await f.Db.DeathMemberSnapshots.ToListAsync());
    }

    [Fact]
    public async Task Database_unique_index_rejects_second_role_even_through_direct_sql()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        await Assert.ThrowsAsync<SqliteException>(() => f.Db.Database.ExecuteSqlRawAsync(
            "INSERT INTO coordinator_positions (position_key, role_code, group_no, member_id, appointed_on, version) SELECT 'group:A', 'group_leader', 'A', member_id, appointed_on, 1 FROM coordinator_positions WHERE position_key = 'chairperson'"));
        Assert.Single(await f.Service.GetMemberRolesAsync());
    }

    [Fact]
    public async Task Demo_reset_clears_coordinator_data_before_members()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        Assert.Equal(2, await new DemoDataMaintenanceService(f.Db).ClearMembersAsync(Now, "tester"));
        Assert.Empty(await f.Db.CoordinatorEvents.ToListAsync());
        Assert.Empty(await f.Db.CoordinatorPositions.ToListAsync());
        Assert.Empty(await f.Db.Members.ToListAsync());
    }
}
