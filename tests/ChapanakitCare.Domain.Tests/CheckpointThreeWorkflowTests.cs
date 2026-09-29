using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointThreeWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 8, 25);
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 3, 0, 0, TimeSpan.Zero);
    private static readonly DeathCertificateDocument Certificate = new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray());

    [Fact]
    public async Task Payable_death_is_atomic_and_returns_advance_plus_net_collection()
    {
        await using var db = await TestDatabase.CreateAsync();
        var deceased = await db.AddMember("00001", 7, Today.AddDays(-1), "มาลี");
        await db.AddMember("00002", 0, Today.AddDays(-20), "หนึ่ง");
        await db.AddMember("00003", -2, Today.AddDays(-20), "สอง");

        var result = await new DeathApplicationService(db.Context).ConfirmAsync(
            new ConfirmDeathCommand("00001", "DC-1", Today, "ชรา", false, null, Certificate), Today, Now, "tester");

        Assert.Equal("payable", result.DeathCase.EligibilityResult);
        Assert.Equal(8_100, result.Calculation.TotalBenefitSatang); // 2*9 baht, fee rounded down to baht, plus 7*9 baht advance.
        Assert.Equal(0, deceased.AdvanceUnitsBalance);
        Assert.Equal(MemberStatus.Deceased, deceased.Status);
        var survivorBalances = await db.Context.Members.Where(x => x.Status == MemberStatus.Normal).OrderBy(x => x.RunNo).Select(x => x.AdvanceUnitsBalance).ToArrayAsync();
        Assert.Equal(new[] { -1, -3 }, survivorBalances);
        Assert.Equal(2, await db.Context.DeathBeneficiarySnapshots.CountAsync());
        Assert.Equal(3, await db.Context.AdvanceLedgerEntries.CountAsync());
    }

    [Theory]
    [InlineData(false, "before_coverage_zero")]
    [InlineData(true, "manual_nonpay_zero")]
    public async Task Nonpay_death_returns_zero_and_does_not_decrement_other_members(bool manual, string expected)
    {
        await using var db = await TestDatabase.CreateAsync();
        await db.AddMember("00001", 30, manual ? Today.AddDays(-1) : Today.AddDays(1), "มาลี");
        var survivor = await db.AddMember("00002", 30, Today.AddDays(-1), "หนึ่ง");

        var result = await new DeathApplicationService(db.Context).ConfirmAsync(
            new ConfirmDeathCommand("00001", "DC-2", Today, "เหตุ", manual, manual ? "เคสไม่จ่ายตามการพิจารณา" : null, Certificate), Today, Now, "tester");

        Assert.Equal(expected, result.DeathCase.EligibilityResult);
        Assert.Equal(0, result.Calculation.TotalBenefitSatang);
        Assert.Equal(30, survivor.AdvanceUnitsBalance);
    }

    [Fact]
    public async Task Reset_restores_only_living_members_and_is_idempotent()
    {
        await using var db = await TestDatabase.CreateAsync();
        var living = await db.AddMember("00001", -3, Today.AddDays(-1), "หนึ่ง");
        var deceased = await db.AddMember("00002", 0, Today.AddDays(-1), "สอง", MemberStatus.Deceased);
        db.Context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            NotificationType = "death_threshold",
            CycleKey = "initial",
            TriggeredBusinessDate = Today,
            TriggeredAtUtc = Now,
            Message = "เตือน"
        });
        await db.Context.SaveChangesAsync();
        var service = new AdvanceResetService(db.Context);

        var first = await service.ResetAsync("manual", "reset-20260825-a", Today, Now, "tester");
        var second = await service.ResetAsync("manual", "reset-20260825-a", Today, Now, "tester");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(30, living.AdvanceUnitsBalance);
        Assert.Equal(0, deceased.AdvanceUnitsBalance);
        Assert.Single(await db.Context.AdvanceResetBatches.ToListAsync());
        Assert.Single(await db.Context.AdvanceResetLines.ToListAsync());
        Assert.Equal("acknowledged", (await db.Context.Notifications.SingleAsync()).State);
    }

    [Fact]
    public async Task Notifications_are_once_per_month_and_once_per_reset_cycle()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.SystemSettings.Single().DeathWarningThreshold = 2;
        var first = await db.AddMember("00001", 0, Today, "หนึ่ง", MemberStatus.Deceased);
        var second = await db.AddMember("00002", 0, Today, "สอง", MemberStatus.Deceased);
        db.Context.DeathCases.AddRange(Death("D1", first.Id), Death("D2", second.Id));
        await db.Context.SaveChangesAsync();
        var service = new NotificationService(db.Context);

        await service.RefreshAsync(new DateOnly(2026, 9, 1), Now, CancellationToken.None);
        Assert.Single(await db.Context.Notifications.ToListAsync());

        var third = await db.AddMember("00003", 0, Today, "สาม", MemberStatus.Deceased);
        db.Context.DeathCases.Add(Death("D3", third.Id));
        await db.Context.SaveChangesAsync();
        await service.RefreshAsync(new DateOnly(2026, 9, 1), Now.AddMinutes(1), CancellationToken.None);
        await service.RefreshAsync(new DateOnly(2026, 9, 1), Now.AddMinutes(2), CancellationToken.None);

        Assert.Equal(2, await db.Context.Notifications.CountAsync());
        Assert.Contains(await db.Context.Notifications.ToListAsync(), x => x.NotificationType == "month_start_reset");
        Assert.Contains(await db.Context.Notifications.ToListAsync(), x => x.NotificationType == "death_threshold" && x.DeathsSinceLatestReset == 3);
    }

    private static DeathCase Death(string no, Guid memberId) => new() { Id = Guid.NewGuid(), DeathCaseNo = no, DeathSequenceNo = int.Parse(no[1..]), MemberId = memberId, RecordedBusinessDate = Today, RecordedAtUtc = Now, DeathCertificateNo = no, DeathCertificateDate = Today, DeathCertificateFileName = "certificate.pdf", DeathCertificateContentType = "application/pdf", DeathCertificatePdf = Certificate.Bytes, DeathCertificateSize = Certificate.Bytes.Length, DeathCertificateSha256 = new string('a', 64), CauseOfDeathText = "x", EligibilityResult = "payable", SettingsRevision = 1, ConfirmedAtUtc = Now, ConfirmedBy = "t" };

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
                new NumberSequence { SequenceKey = "death_case_no", NextValue = 1, Width = 5, Prefix = "D", UpdatedAtUtc = Now },
                new NumberSequence { SequenceKey = "reset_no", NextValue = 1, Width = 5, Prefix = "R", UpdatedAtUtc = Now });
            await context.SaveChangesAsync(); return new TestDatabase(connection, context);
        }
        public async Task<Member> AddMember(string runNo, int units, DateOnly coverage, string first, MemberStatus status = MemberStatus.Normal)
        {
            var m = new Member { Id = Guid.NewGuid(), RunNo = runNo, Title = "นาง", FirstName = first, LastName = "ทดสอบ", Gender = "หญิง", District = "ร้องกวาง", Province = "แพร่", ApplicationDate = Today.AddDays(-200), ApprovalDate = Today.AddDays(-181), CoverageStartDate = coverage, Status = status, AdvanceUnitsBalance = units, CreatedAtUtc = Now, UpdatedAtUtc = Now, CreatedBy = "t", UpdatedBy = "t" };
            Context.Members.Add(m);
            if (status == MemberStatus.Normal && runNo == "00001") Context.MemberBeneficiaries.AddRange(Beneficiary(m.Id, 1), Beneficiary(m.Id, 2));
            await Context.SaveChangesAsync(); return m;
        }
        private static MemberBeneficiary Beneficiary(Guid id, int slot) => new() { Id = Guid.NewGuid(), MemberId = id, SlotNo = slot, FirstName = $"ผู้รับ{slot}", LastName = "เงิน", IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now, CreatedBy = "t", UpdatedBy = "t" };
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
