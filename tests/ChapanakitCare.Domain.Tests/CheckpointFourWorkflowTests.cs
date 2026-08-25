using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointFourWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 8, 25);
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Demo_import_adds_new_fixture_records_when_other_members_already_exist()
    {
        await using var db = await TestDatabase.CreateAsync();
        var memberService = new MemberApplicationService(db.Context);
        await memberService.RegisterAsync(Registration("เจ้าหน้าที่", "9000000000001"), Today, Now, "tester");

        var imported = await new DemoImportService(db.Context).ImportAsync(
            [Registration("สาธิตหนึ่ง", "1000000000001"), Registration("สาธิตสอง", "1000000000002")],
            Today, Now.AddMinute(), "demo");

        Assert.Equal(2, imported);
        Assert.Equal(3, await db.Context.Members.CountAsync());
    }

    [Fact]
    public async Task Reimport_skips_matching_fixture_identity_without_blocking_other_new_records()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new DemoImportService(db.Context);
        await service.ImportAsync([Registration("เดิม", "1000000000001")], Today, Now, "demo");

        var imported = await service.ImportAsync(
            [Registration("เดิม", "1000000000001"), Registration("ใหม่", "1000000000002")],
            Today, Now.AddMinute(), "demo");

        Assert.Equal(1, imported);
        Assert.Equal(2, await db.Context.Members.CountAsync());
    }

    [Fact]
    public async Task Reimport_skips_blank_id_fixture_using_stable_person_fields()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new DemoImportService(db.Context);
        var blankId = Registration("ไม่มีเลขบัตร", "") with { PersonalIdCard = null };

        await service.ImportAsync([blankId], Today, Now, "demo");
        var imported = await service.ImportAsync([blankId], Today, Now.AddMinute(), "demo");

        Assert.Equal(0, imported);
        Assert.Single(await db.Context.Members.ToListAsync());
    }

    [Fact]
    public async Task Clear_demo_members_removes_all_dependent_operational_data_and_restarts_member_numbers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var member = await new MemberApplicationService(db.Context).RegisterAsync(Registration("ล้าง", "1000000000001"), Today, Now, "tester");
        db.Context.AuditFieldChanges.Add(new AuditFieldChange { Id = Guid.NewGuid(), AuditEventId = (await db.Context.AuditEvents.FirstAsync()).Id, FieldName = "x" });
        await db.Context.SaveChangesAsync();

        var removed = await new DemoDataMaintenanceService(db.Context).ClearMembersAsync(Now.AddMinute(), "tester");

        Assert.Equal(1, removed);
        Assert.Empty(await db.Context.Members.ToListAsync());
        Assert.Empty(await db.Context.MemberBeneficiaries.ToListAsync());
        Assert.Empty(await db.Context.AdvanceLedgerEntries.ToListAsync());
        Assert.Empty(await db.Context.AuditEvents.ToListAsync());
        Assert.Equal(1, (await db.Context.NumberSequences.SingleAsync(x => x.SequenceKey == "member_run_no")).NextValue);
        Assert.NotNull(await db.Context.SystemSettings.SingleAsync());
    }

    [Fact]
    public async Task Death_preview_is_read_only_and_returns_reference_breakdown_with_equal_recipient_shares()
    {
        await using var db = await TestDatabase.CreateAsync();
        var member = await new MemberApplicationService(db.Context).RegisterAsync(Registration("ผู้เสียชีวิต", "1000000000001", twoBeneficiaries: true), Today.AddDays(-200), Now, "tester");
        member.CoverageStartDate = Today;
        await new MemberApplicationService(db.Context).RegisterAsync(Registration("ผู้ร่วม", "1000000000002"), Today, Now.AddMinute(), "tester");
        await db.Context.SaveChangesAsync();

        var preview = await new DeathApplicationService(db.Context).PreviewAsync("00001", false, Today);

        Assert.Equal(1, preview.Calculation.ContributorCount);
        Assert.Equal(1_500, preview.Calculation.GrossCollectionSatang);
        Assert.Equal(60, preview.Calculation.ServiceFeeSatang);
        Assert.Equal(46_440, preview.Calculation.TotalBenefitSatang);
        Assert.Equal(new long[] { 23_220, 23_220 }, preview.BeneficiarySharesSatang);
        Assert.Equal(2, preview.Beneficiaries.Count);
        Assert.Empty(await db.Context.DeathCases.ToListAsync());
        Assert.Equal(MemberStatus.Normal, member.Status);
    }

    [Fact]
    public void Audit_action_label_never_exposes_internal_death_code()
    {
        Assert.Equal("บันทึกการเสียชีวิต", AuditActionLabels.ToThai("death.confirmed"));
    }

    private static RegisterMemberCommand Registration(string first, string personalId, bool twoBeneficiaries = false) => new(
        "นาย", first, "ทดสอบ", "ชาย", personalId, new DateOnly(1980, 1, 1), "1", "บ้าน", "1", "ร้องกวาง", "54140", "0811111111", "001", Today, Today,
        twoBeneficiaries ? [Beneficiary(1), Beneficiary(2)] : [Beneficiary(1)]);

    private static BeneficiaryCommand Beneficiary(int slot) => new(slot, "นาง", $"ผู้รับ{slot}", "ทดสอบ", "ญาติ", $"2{slot}00000000000", "0822222222", "2", "บ้าน", "2", "ร้องกวาง", "ร้องกวาง", "แพร่", "54140");

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private TestDatabase(SqliteConnection connection, AppDbContext context) { this.connection = connection; Context = context; }
        public AppDbContext Context { get; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            context.NumberSequences.AddRange(
                new NumberSequence { SequenceKey = "member_run_no", NextValue = 1, Width = 5, UpdatedAtUtc = Now },
                new NumberSequence { SequenceKey = "death_case_no", Prefix = "D", NextValue = 1, Width = 5, UpdatedAtUtc = Now },
                new NumberSequence { SequenceKey = "reset_no", Prefix = "R", NextValue = 1, Width = 5, UpdatedAtUtc = Now });
            await context.SaveChangesAsync(); return new(connection, context);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
}

file static class DateExtensions
{
    public static DateTimeOffset AddMinute(this DateTimeOffset value) => value.AddMinutes(1);
}
