using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class ActivityHistoryTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private AppDbContext db = null!;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Members.Add(TestData.Member());
        db.NumberSequences.Add(new() { SequenceKey = "member_run_no", NextValue = 2, Width = 5 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Advance_reset_is_audited_once_and_reads_do_not_add_history()
    {
        var service = new AdvanceResetService(db);
        await service.ResetAsync("manual", "once", new(2026, 9, 8), DateTimeOffset.UtcNow, "เจ้าหน้าที่");
        await service.ResetAsync("manual", "once", new(2026, 9, 8), DateTimeOffset.UtcNow, "เจ้าหน้าที่");
        var audit = Assert.Single(await db.AuditEvents.ToListAsync());
        Assert.Equal("advance.reset", audit.Action);
        await new MemberApplicationService(db).SearchAsync(new(null, null, null, null, null));
        await new TablePreferenceService(db).SaveAsync("local-user", "member-library", [], [], DateTimeOffset.UtcNow);
        Assert.Single(await db.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Clearing_demo_members_preserves_history_and_records_the_clear()
    {
        var member = await db.Members.SingleAsync();
        var audit = new AuditEvent { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Action = "member.created",
            MemberId = member.Id, EntityId = member.Id.ToString(), EntityType = "member", ActorUserId = "local-user",
            ActorDisplayName = "tester", MachineName = "test", AppVersion = "test", OccurredAtUtc = DateTimeOffset.UtcNow };
        db.AuditEvents.Add(audit);
        db.AuditFieldChanges.Add(new() { Id = Guid.NewGuid(), AuditEventId = audit.Id, FieldName = "ชื่อ", NewValueDisplay = "ทดสอบ" });
        await db.SaveChangesAsync();
        await new DemoDataMaintenanceService(db).ClearMembersAsync(DateTimeOffset.UtcNow, "tester");
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Members.ToListAsync());
        Assert.Contains(await db.AuditEvents.ToListAsync(), x => x.Id == audit.Id && x.MemberId == member.Id);
        Assert.Contains(await db.AuditEvents.ToListAsync(), x => x.Action == "demo.members_cleared");
        Assert.Single(await db.AuditFieldChanges.ToListAsync());
    }

    [Fact]
    public async Task Audit_entries_cannot_be_edited_in_place()
    {
        var service = new SettingsApplicationService(db);
        await service.SaveAsync(new(null, 400, 900, 30, 180, 365, 25), DateTimeOffset.UtcNow, "test");
        (await db.AuditEvents.SingleAsync()).Action = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Beneficiary_address_edits_have_old_and_new_values_in_member_history()
    {
        var beneficiary = TestData.Beneficiary(1);
        beneficiary.HouseNo = "12";
        db.MemberBeneficiaries.Add(beneficiary);
        await db.SaveChangesAsync();
        var form = ChapanakitCare.Web.Pages.Members.MemberFormInput.From(await db.Members.SingleAsync(), [beneficiary]);
        form.Beneficiary1.HouseNo = "34";
        await new MemberApplicationService(db).UpdateAsync(beneficiary.MemberId, form.ToUpdateCommand(), DateTimeOffset.UtcNow, "test");
        var change = Assert.Single(await db.AuditFieldChanges.ToListAsync(), x => x.FieldName == "Beneficiary1.HouseNo");
        Assert.Equal("12", change.OldValueDisplay);
        Assert.Equal("34", change.NewValueDisplay);
    }

    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
}
