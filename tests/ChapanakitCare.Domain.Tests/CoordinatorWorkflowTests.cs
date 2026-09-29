using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class CoordinatorWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 9, 4);
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Vacancies_include_chair_and_existing_groups_without_writing_on_read()
    {
        await using var fixture = await Fixture.CreateAsync();
        var positions = await fixture.Service.GetPositionsAsync();
        Assert.Equal(new[] { "chairperson", "group:A", "group:B" }, positions.Select(x => x.Key));
        Assert.All(positions, x => { Assert.Null(x.MemberId); Assert.Equal(0, x.Version); });
        Assert.Empty(await fixture.Db.CoordinatorPositions.ToListAsync());
    }

    [Fact]
    public async Task Appoint_replace_and_end_persist_history_snapshots_and_current_projection()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        Assert.Equal(f.First.Id, (await f.Service.GetPositionsAsync()).Single(x => x.Key == "chairperson").MemberId);
        await f.Change("chairperson", "replace", f.Second.Id, 1, f.First.Id, "เปลี่ยนผู้รับผิดชอบ");
        f.First.FirstName = "ชื่อใหม่";
        await f.Db.SaveChangesAsync();
        await f.Change("chairperson", "end", null, 2, f.Second.Id, "สิ้นสุดหน้าที่");
        var history = (await f.Service.GetHistoryAsync()).OrderBy(x => x.PositionVersion).ToList();
        Assert.Equal(new[] { "appoint", "replace", "end" }, history.Select(x => x.Action));
        Assert.Contains("ทดสอบ", history[0].MemberName);
        Assert.Contains("ทดสอบ", history[1].PreviousMemberName);
        Assert.Equal(Today, history[0].EffectiveDate);
        Assert.Null((await f.Service.GetPositionsAsync()).Single(x => x.Key == "chairperson").MemberId);
        Assert.Empty(await f.Service.GetMemberRolesAsync());
        Assert.Equal(MemberStatus.Normal, f.Second.Status);
        Assert.Equal(30, f.Second.AdvanceUnitsBalance);
        Assert.Equal(3, await f.Db.AuditEvents.CountAsync(x => x.EntityType == "coordinator_position"));
        var replacementAudit = await f.Db.AuditEvents.SingleAsync(x => x.Action == "coordinator.replace");
        Assert.Contains("00001", replacementAudit.Reason);
        Assert.Contains("00002", replacementAudit.Reason);
        Assert.Contains("เปลี่ยนผู้รับผิดชอบ", replacementAudit.Reason);
    }

    [Fact]
    public async Task One_member_cannot_hold_chair_and_group_leader_roles()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Change("group:A", "appoint", f.First.Id, 0, null));
        Assert.Single(await f.Db.CoordinatorEvents.ToListAsync());
        Assert.Single(await f.Service.GetMemberRolesAsync());
    }

    [Theory]
    [InlineData("group:B")]
    [InlineData("group:missing")]
    [InlineData("treasurer")]
    public async Task Wrong_group_or_unknown_position_cannot_be_forged(string position)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Change(position, "appoint", f.First.Id, 0, null));
        Assert.Empty(await f.Db.CoordinatorEvents.ToListAsync());
    }

    [Theory]
    [InlineData(MemberStatus.Deceased, false)]
    [InlineData(MemberStatus.Resigned, false)]
    [InlineData(MemberStatus.Normal, true)]
    public async Task Ineligible_members_cannot_be_assigned(MemberStatus status, bool archived)
    {
        await using var f = await Fixture.CreateAsync();
        f.First.Status = status;
        if (archived) { f.First.ArchivedAtUtc = Now; f.First.ArchiveReason = "ทดสอบ"; }
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Change("chairperson", "appoint", f.First.Id, 0, null));
    }

    [Fact]
    public async Task Candidate_search_enforces_group_and_excludes_members_already_holding_roles()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.Second.Id, 0, null);
        var matches = await f.Service.SearchCandidatesAsync("group:A", "00001");
        Assert.Equal(f.First.Id, Assert.Single(matches).Id);
        Assert.Empty(await f.Service.SearchCandidatesAsync("group:B", null));
        Assert.Empty(await f.Service.SearchCandidatesAsync("group:A", "00002"));
    }

    [Theory]
    [InlineData("appoint", 1, true, null)]
    [InlineData("replace", 0, true, "stale")]
    [InlineData("replace", 1, false, "wrong incumbent")]
    [InlineData("replace", 1, true, null)]
    [InlineData("end", 1, true, " ")]
    [InlineData("invalid", 1, true, "reason")]
    public async Task Explicit_action_incumbent_version_and_reason_protect_existing_selection(string action, int version, bool correctIncumbent, string? reason)
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Change("chairperson", action,
            action == "end" ? null : f.Second.Id, version, correctIncumbent ? f.First.Id : f.Second.Id, reason));
        Assert.Equal(f.First.Id, (await f.Service.GetPositionsAsync()).First().MemberId);
        Assert.Single(await f.Db.CoordinatorEvents.ToListAsync());
    }

    [Fact]
    public async Task Vacancy_version_survives_end_and_prevents_old_form_reuse()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        await f.Change("chairperson", "end", null, 1, f.First.Id, "สิ้นสุด");
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Change("chairperson", "appoint", f.Second.Id, 0, null));
        await f.Change("chairperson", "appoint", f.Second.Id, 2, null);
        Assert.Equal(3, (await f.Service.GetPositionsAsync()).First().Version);
    }

    [Fact]
    public async Task Failed_history_write_does_not_replace_the_current_holder()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Change("chairperson", "appoint", f.First.Id, 0, null);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_role_replace BEFORE INSERT ON coordinator_events WHEN NEW.action = 'replace' BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        await Assert.ThrowsAsync<MemberValidationException>(() => f.Change("chairperson", "replace", f.Second.Id, 1, f.First.Id, "ทดสอบ rollback"));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(f.First.Id, (await f.Db.CoordinatorPositions.SingleAsync()).MemberId);
        Assert.Single(await f.Db.CoordinatorEvents.ToListAsync());
        Assert.Single(await f.Db.AuditEvents.ToListAsync());
    }

    internal sealed class Fixture(SqliteConnection connection, AppDbContext db, Member first, Member second) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;
        public Member First { get; } = first;
        public Member Second { get; } = second;
        public CoordinatorApplicationService Service => new(Db);
        public Task Change(string key, string action, Guid? member, int version, Guid? incumbent, string? reason = null) =>
            Service.ChangeAsync(new(key, action, member, version, incumbent, reason), Today, Now, "tester");
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var first = TestData.Member(); first.GroupNo = "A";
            var second = TestData.Member(); second.Id = Guid.NewGuid(); second.RunNo = "00002"; second.GroupNo = "B"; second.FirstName = "คนที่สอง";
            db.Members.AddRange(first, second);
            await db.SaveChangesAsync();
            return new(connection, db, first, second);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
