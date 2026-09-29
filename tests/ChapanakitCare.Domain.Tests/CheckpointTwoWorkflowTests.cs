using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointTwoWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 8, 25);
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 2, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Registering_member_assigns_run_number_and_creates_opening_records()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MemberApplicationService(database.Context);

        var member = await service.RegisterAsync(ValidRegistration(), Today, Now, "เจ้าหน้าที่ทดสอบ");

        Assert.Equal("00001", member.RunNo);
        Assert.Equal(new DateOnly(2027, 2, 21), member.CoverageStartDate);
        Assert.Equal(30, member.AdvanceUnitsBalance);
        Assert.Single(await database.Context.MemberStatusEvents.ToListAsync());
        var opening = Assert.Single(await database.Context.AdvanceLedgerEntries.ToListAsync());
        Assert.Equal("opening_30", opening.EntryType);
        Assert.Equal(30, opening.BalanceAfter);
        Assert.Single(await database.Context.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Registration_uses_the_current_launcher_constants()
    {
        await using var database = await TestDatabase.CreateAsync();
        var settings = await database.Context.SystemSettings.SingleAsync();
        settings.CoverageWaitDays = 200;
        settings.ResetTargetUnits = 35;
        await database.Context.SaveChangesAsync();
        var service = new MemberApplicationService(database.Context);

        var member = await service.RegisterAsync(ValidRegistration(), Today, Now, "เจ้าหน้าที่ทดสอบ");

        Assert.Equal(Today.AddDays(200), member.CoverageStartDate);
        Assert.Equal(35, member.AdvanceUnitsBalance);
        Assert.Equal(35, (await database.Context.AdvanceLedgerEntries.SingleAsync()).BalanceAfter);
    }

    [Fact]
    public async Task Registration_calculates_coverage_from_approval_date_not_application_date()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MemberApplicationService(database.Context);
        var command = ValidRegistration() with
        {
            ApplicationDate = new DateOnly(2026, 1, 1),
            ApprovalDate = new DateOnly(2026, 1, 15)
        };

        var member = await service.RegisterAsync(command, Today, Now, "เจ้าหน้าที่ทดสอบ");

        Assert.Equal(new DateOnly(2026, 7, 14), member.CoverageStartDate);
    }

    [Fact]
    public async Task Editing_member_recalculates_coverage_from_approval_date_not_application_date()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MemberApplicationService(database.Context);
        var registered = await service.RegisterAsync(ValidRegistration(), Today, Now, "เจ้าหน้าที่ทดสอบ");

        var updated = await service.UpdateAsync(
            registered.Id,
            new UpdateMemberCommand(
                registered.Version,
                registered.Title,
                registered.FirstName,
                registered.LastName,
                registered.Gender,
                registered.PersonalIdCard,
                registered.BirthDate,
                registered.HouseNo,
                registered.Under,
                registered.Moo,
                registered.Subdistrict,
                registered.PostalCode,
                registered.Mobile,
                registered.GroupNo,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 15),
                []),
            Now.AddHours(1),
            "เจ้าหน้าที่ทดสอบ");

        Assert.Equal(new DateOnly(2026, 7, 14), updated.CoverageStartDate);
    }

    [Fact]
    public async Task Registering_member_rejects_more_than_two_beneficiaries()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MemberApplicationService(database.Context);
        var command = ValidRegistration() with
        {
            Beneficiaries =
            [
                BeneficiaryInput(1),
                BeneficiaryInput(2),
                BeneficiaryInput(3)
            ]
        };

        var exception = await Assert.ThrowsAsync<MemberValidationException>(
            () => service.RegisterAsync(command, Today, Now, "เจ้าหน้าที่ทดสอบ"));

        Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        Assert.Empty(await database.Context.Members.ToListAsync());
    }

    [Fact]
    public async Task Editing_member_preserves_run_number_and_records_changed_fields()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MemberApplicationService(database.Context);
        var registered = await service.RegisterAsync(ValidRegistration(), Today, Now, "ผู้เพิ่ม");

        var updated = await service.UpdateAsync(
            registered.Id,
            new UpdateMemberCommand(
                registered.Version,
                registered.Title,
                registered.FirstName,
                registered.LastName,
                registered.Gender,
                registered.PersonalIdCard,
                registered.BirthDate,
                registered.HouseNo,
                registered.Under,
                registered.Moo,
                "ร้องเข็ม",
                registered.PostalCode,
                registered.Mobile,
                registered.GroupNo,
                registered.ApplicationDate,
                registered.ApprovalDate,
                []),
            Now.AddHours(1),
            "ผู้แก้ไข");

        Assert.Equal("00001", updated.RunNo);
        Assert.Equal(2, updated.Version);
        Assert.Equal("ร้องเข็ม", updated.Subdistrict);
        var editAudit = await database.Context.AuditEvents.SingleAsync(value => value.Action == "member.updated");
        var change = await database.Context.AuditFieldChanges.SingleAsync(value => value.AuditEventId == editAudit.Id && value.FieldName == "Subdistrict");
        Assert.Equal("Subdistrict", change.FieldName);
        Assert.Equal("แม่ยางตาล", change.OldValueDisplay);
        Assert.Equal("ร้องเข็ม", change.NewValueDisplay);
    }

    [Fact]
    public async Task Member_search_combines_record_text_date_range_and_status()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new MemberApplicationService(database.Context);
        await service.RegisterAsync(ValidRegistration(), Today, Now, "ผู้เพิ่ม");
        await service.RegisterAsync(
            ValidRegistration() with
            {
                FirstName = "สมชาย",
                LastName = "ใจดี",
                PersonalIdCard = "2222222222222",
                ApplicationDate = new DateOnly(2026, 8, 1)
            },
            Today,
            Now.AddMinutes(1),
            "ผู้เพิ่ม");

        var result = await service.SearchAsync(new MemberSearchQuery(
            "สมชาย",
            MemberDateField.Application,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            MemberStatus.Normal));

        var member = Assert.Single(result);
        Assert.Equal("00002", member.RunNo);
    }

    [Fact]
    public async Task Saving_settings_increments_revision_and_writes_audit_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new SettingsApplicationService(database.Context);

        var settings = await service.SaveAsync(
            new SettingsCommand(null, 400, 1_500, 30, 180, 365, 25),
            Now,
            "ผู้ดูแล");

        Assert.Equal(2, settings.SettingsRevision);
        Assert.Equal(1_500, settings.WelfarePerMemberSatang);
        Assert.Contains(
            await database.Context.AuditEvents.ToListAsync(),
            value => value.Action == "settings.updated");
    }

    [Fact]
    public async Task Table_preference_can_be_saved_and_reset_to_default()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new TablePreferenceService(database.Context);

        await service.SaveAsync(
            "local-user",
            "member-library",
            ["name", "runNo"],
            ["personalIdCard"],
            Now);
        var saved = await service.GetAsync("local-user", "member-library");
        await service.ResetAsync("local-user", "member-library");
        var reset = await service.GetAsync("local-user", "member-library");

        Assert.Equal(["name", "runNo"], saved.ColumnOrder);
        Assert.Equal(["personalIdCard"], saved.HiddenColumns);
        Assert.Empty(reset.ColumnOrder);
        Assert.Empty(reset.HiddenColumns);
    }

    [Fact]
    public async Task Demo_import_only_runs_into_an_empty_disposable_database()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new DemoImportService(database.Context);
        var records = new[]
        {
            ValidRegistration(),
            ValidRegistration() with { FirstName = "สมชาย", PersonalIdCard = "2222222222222" }
        };

        var first = await service.ImportAsync(records, Today, Now, "demo-import");
        var second = await service.ImportAsync(records, Today, Now.AddMinutes(1), "demo-import");

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, await database.Context.Members.CountAsync());
    }

    private static RegisterMemberCommand ValidRegistration() => new(
        "นาย",
        "ทดสอบ",
        "สมาชิก",
        "ชาย",
        "1111111111111",
        new DateOnly(1980, 1, 1),
        "11",
        null,
        "1",
        "แม่ยางตาล",
        "54140",
        "0811111111",
        "001",
        Today,
        Today,
        [BeneficiaryInput(1)]);

    private static BeneficiaryCommand BeneficiaryInput(int slot) => new(
        slot,
        "นางสาว",
        $"ผู้รับ{slot}",
        "ทดสอบ",
        "บุตร",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, AppDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        internal AppDbContext Context { get; }

        internal static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.NumberSequences.Add(new NumberSequence
            {
                SequenceKey = "member_run_no",
                NextValue = 1,
                Width = 5,
                UpdatedAtUtc = Now
            });
            await context.SaveChangesAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
