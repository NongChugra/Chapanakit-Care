using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointFiveWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 8, 25);
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 5, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = "%PDF-1.4\nsynthetic death certificate\n%%EOF"u8.ToArray();

    [Fact]
    public async Task Member_search_combines_location_and_billing_group_filters()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.Members.AddRange(
            Member("00001", "ร้องเข็ม", "2", "บ้านใหม่", "A"),
            Member("00002", "ร้องกวาง", "2", "บ้านใหม่", "A"),
            Member("00003", "ร้องเข็ม", "3", "บ้านเดิม", "B"));
        await db.Context.SaveChangesAsync();

        var rows = await new MemberApplicationService(db.Context).SearchAsync(new MemberSearchQuery(
            Text: null, DateField: null, From: null, To: null, Status: null,
            Subdistrict: "ร้องเข็ม", District: "ร้องกวาง", Moo: "2", GroupNo: "A", Under: "บ้านใหม่"));

        Assert.Equal(["00001"], rows.Select(value => value.RunNo).ToArray());
    }

    [Fact]
    public async Task Member_global_search_includes_group_and_full_address_fields()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.Members.Add(Member("00001", "ร้องเข็ม", "2", "บ้านเฉพาะคำ", "กลุ่มพิเศษ"));
        await db.Context.SaveChangesAsync();

        var service = new MemberApplicationService(db.Context);
        Assert.Single(await service.SearchAsync(new MemberSearchQuery("กลุ่มพิเศษ", null, null, null, null)));
        Assert.Single(await service.SearchAsync(new MemberSearchQuery("บ้านเฉพาะคำ", null, null, null, null)));
        Assert.Single(await service.SearchAsync(new MemberSearchQuery("ร้องกวาง", null, null, null, null)));
    }

    [Fact]
    public async Task Member_registration_persists_editable_district_and_province()
    {
        await using var db = await TestDatabase.CreateAsync();

        var member = await new MemberApplicationService(db.Context).RegisterAsync(
            Registration("0000000000001") with { District = "เมืองแพร่", Province = "แพร่" }, Today, Now, "tester");

        Assert.Equal("เมืองแพร่", member.District);
        Assert.Equal("แพร่", member.Province);
    }

    [Fact]
    public async Task Table_preference_round_trips_combined_field_components()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new TablePreferenceService(db.Context);

        await service.SaveAsync("local", "member-library", ["name", "address"], ["gender"],
            ["name.title", "name.firstName", "address.houseNo", "address.moo", "address.subdistrict"],
            "name", "desc", Now);
        var saved = await service.GetAsync("local", "member-library");

        Assert.Equal(["name.title", "name.firstName", "address.houseNo", "address.moo", "address.subdistrict"], saved.VisibleComponents);
        Assert.Equal("name", saved.SortColumn);
        Assert.Equal("desc", saved.SortDirection);
    }

    [Fact]
    public void Registration_rejects_birth_date_after_application_date()
    {
        var issues = InteractiveMemberRegistrationPolicy.Validate(
            Registration("0000000000001") with { BirthDate = Today.AddDays(1) });

        Assert.Contains(issues, issue => issue.Field == "BirthDate");
    }

    [Fact]
    public async Task Settings_reject_negative_registration_fee()
    {
        await using var db = await TestDatabase.CreateAsync();
        var command = new SettingsCommand(-1, 400, 1_500, 30, 180, 365, 25);

        await Assert.ThrowsAsync<MemberValidationException>(() =>
            new SettingsApplicationService(db.Context).SaveAsync(command, Now, "tester"));
    }

    [Fact]
    public async Task Death_preview_exposes_configured_special_nonpay_safeguard()
    {
        await using var db = await TestDatabase.CreateAsync();
        var member = Member("00001", "ร้องกวาง", "1", "บ้านร้องกวาง", "001");
        member.CoverageStartDate = Today.AddDays(-100);
        db.Context.Members.Add(member);
        db.Context.MemberBeneficiaries.Add(Beneficiary(member.Id));
        await db.Context.SaveChangesAsync();

        var preview = await new DeathApplicationService(db.Context).PreviewAsync("00001", false, Today);

        Assert.True(preview.IsWithinSpecialNonPayWindow);
        Assert.Equal(365, preview.SpecialNonPayWindowDays);
        Assert.Equal(400, preview.ServiceFeeBasisPoints);
    }

    [Fact]
    public async Task Death_confirmation_stores_certificate_pdf_atomically_and_advances_display_number()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.Members.Add(Member("00001", "ร้องกวาง", "1", "บ้านร้องกวาง", "001"));
        db.Context.MemberBeneficiaries.Add(Beneficiary(db.Context.Members.Local.Single().Id));
        await db.Context.SaveChangesAsync();
        var service = new DeathApplicationService(db.Context);

        Assert.Equal("D00001", await service.GetNextDeathCaseNoAsync());
        var result = await service.ConfirmAsync(new ConfirmDeathCommand(
            "00001", "CERT-001", new DateOnly(2026, 8, 24), "ชรา", false, null,
            new DeathCertificateDocument("death-certificate.pdf", "application/pdf", Pdf)), Today, Now, "tester");
        var stored = await service.GetCertificateAsync(result.DeathCase.Id);

        Assert.Equal(new DateOnly(2026, 8, 24), result.DeathCase.DeathCertificateDate);
        Assert.Equal("death-certificate.pdf", stored.FileName);
        Assert.Equal(Pdf, stored.Bytes);
        Assert.Equal(64, result.DeathCase.DeathCertificateSha256.Length);
        Assert.Equal("D00002", await service.GetNextDeathCaseNoAsync());
    }

    [Fact]
    public async Task Death_confirmation_rejects_a_file_that_only_claims_to_be_pdf()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.Members.Add(Member("00001", "ร้องกวาง", "1", "บ้านร้องกวาง", "001"));
        db.Context.MemberBeneficiaries.Add(Beneficiary(db.Context.Members.Local.Single().Id));
        await db.Context.SaveChangesAsync();

        var action = () => new DeathApplicationService(db.Context).ConfirmAsync(new ConfirmDeathCommand(
            "00001", "CERT-001", Today, "ชรา", false, null,
            new DeathCertificateDocument("fake.pdf", "application/pdf", "not a pdf"u8.ToArray())), Today, Now, "tester");

        var error = await Assert.ThrowsAsync<MemberValidationException>(action);
        Assert.Contains("PDF", error.Message);
        Assert.Empty(await db.Context.DeathCases.ToListAsync());
    }

    [Fact]
    public async Task Backup_creates_an_integrity_checked_sqlite_copy_and_logs_its_hash()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.Members.Add(Member("00001", "ร้องกวาง", "1", "บ้านร้องกวาง", "001"));
        await db.Context.SaveChangesAsync();
        var directory = Path.Combine(Path.GetTempPath(), $"chapanakit-backup-{Guid.NewGuid():N}");
        try
        {
            var result = await new BackupApplicationService(db.Context).CreateAsync(directory, Now, "tester");

            Assert.True(File.Exists(result.FullPath));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(result.FullPath))).ToLowerInvariant(), result.Sha256);
            var logged = await db.Context.BackupRuns.SingleAsync();
            Assert.Equal(result.Sha256, logged.Sha256);

            await using (var copy = new SqliteConnection($"Data Source={result.FullPath};Pooling=False"))
            {
                await copy.OpenAsync();
                await using var integrity = copy.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check;";
                Assert.Equal("ok", await integrity.ExecuteScalarAsync());
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Synthetic_demo_fixture_contains_40_complete_mandatory_member_records()
    {
        await using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-members.json"));
        var records = await JsonSerializer.DeserializeAsync<RegisterMemberCommand[]>(input, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(records);
        Assert.Equal(40, records.Length);
        // Historical members remain editable regardless of today's new-admission age rule.
        Assert.All(records, record => Assert.Empty(InteractiveMemberRegistrationPolicy.Validate(new UpdateMemberCommand(
            1, record.Title, record.FirstName, record.LastName, record.Gender, record.PersonalIdCard, record.BirthDate,
            record.HouseNo, record.Under, record.Moo, record.Subdistrict, record.PostalCode, record.Mobile, record.GroupNo,
            record.ApplicationDate, record.ApprovalDate, record.Beneficiaries, record.District, record.Province))));
        Assert.Equal(40, records.Select(value => value.PersonalIdCard).Distinct().Count());
    }

    private static Member Member(string runNo, string subdistrict, string moo, string under, string group) => new()
    {
        Id = Guid.NewGuid(),
        RunNo = runNo,
        Title = "นาง",
        FirstName = $"สมาชิก{runNo}",
        LastName = "ทดสอบ",
        Gender = "หญิง",
        PersonalIdCard = $"1{runNo.PadLeft(12, '0')}"[..13],
        BirthDate = new DateOnly(1960, 1, 1),
        HouseNo = "1",
        Moo = moo,
        Under = under,
        Subdistrict = subdistrict,
        District = "ร้องกวาง",
        Province = "แพร่",
        PostalCode = "54140",
        Mobile = "0811111111",
        GroupNo = group,
        ApplicationDate = Today.AddDays(-400),
        ApprovalDate = Today.AddDays(-380),
        CoverageStartDate = Today.AddDays(-200),
        AdvanceUnitsBalance = 30,
        Status = MemberStatus.Normal,
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now,
        CreatedBy = "test",
        UpdatedBy = "test"
    };

    private static MemberBeneficiary Beneficiary(Guid memberId) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        SlotNo = 1,
        Title = "นาย",
        FirstName = "ผู้รับ",
        LastName = "ทดสอบ",
        Relationship = "บุตร",
        PersonalIdCard = "2000000000001",
        Mobile = "0822222222",
        HouseNo = "2",
        Under = "บ้านร้องกวาง",
        Moo = "1",
        Subdistrict = "ร้องกวาง",
        District = "ร้องกวาง",
        Province = "แพร่",
        PostalCode = "54140",
        IsActive = true,
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now,
        CreatedBy = "test",
        UpdatedBy = "test"
    };

    private static RegisterMemberCommand Registration(string id) => new(
        "นาย", "สมาชิก", "ครบถ้วน", "ชาย", id, new DateOnly(1980, 1, 1), "11", "บ้านร้องกวาง", "1", "ร้องกวาง",
        "54140", "0811111111", "001", Today, Today,
        [new BeneficiaryCommand(1, "นาง", "ผู้รับ", "ครบถ้วน", "บุตร", "2000000000001", "0822222222", "12", "บ้านร้องกวาง", "2", "ร้องกวาง", "ร้องกวาง", "แพร่", "54140")],
        "ร้องกวาง", "แพร่");

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private TestDatabase(SqliteConnection connection, AppDbContext context) { this.connection = connection; Context = context; }
        public AppDbContext Context { get; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            context.NumberSequences.AddRange(
                new NumberSequence { SequenceKey = "member_run_no", NextValue = 1, Width = 5, UpdatedAtUtc = Now },
                new NumberSequence { SequenceKey = "death_case_no", Prefix = "D", NextValue = 1, Width = 5, UpdatedAtUtc = Now });
            await context.SaveChangesAsync();
            return new TestDatabase(connection, context);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
