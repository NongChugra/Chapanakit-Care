using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointFourReportTests
{
    [Fact]
    public void Report_page_keeps_month_query_value_in_gregorian_format_under_thai_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
            var page = new ChapanakitCare.Web.Pages.Reports.IndexModel(null!);
            page.From = new DateOnly(2025, 6, 1);

            Assert.Equal("2025-06", page.DefaultMonth);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Member_by_manager_rows_keep_member_details_only_on_the_first_beneficiary_line()
    {
        var rows = MemberByManagerReportRows.Expand(new MemberByManagerReportSource(
            "00001", "นาย", "ทดสอบ", "รายงาน", "1234567890123",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), new DateOnly(2027, 2, 1),
            new DateOnly(1980, 1, 1), "1 ม.2 ต.บ้านกลาง", ["นาง หนึ่ง", "นาย สอง", "นาง สาม"]));

        Assert.Equal(3, rows.Count);
        Assert.Equal("1", rows[0].SequenceNo);
        Assert.Equal("00001", rows[0].RunNo);
        Assert.Equal("นาง หนึ่ง", rows[0].BeneficiaryName);
        Assert.Equal("", rows[1].SequenceNo);
        Assert.Equal("", rows[1].RunNo);
        Assert.Equal("", rows[1].MemberName);
        Assert.Equal("", rows[1].Address);
        Assert.Equal("นาย สอง", rows[1].BeneficiaryName);
        Assert.Equal("นาง สาม", rows[2].BeneficiaryName);
    }

    [Fact]
    public async Task Three_fixed_reports_generate_real_pdf_documents()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new ReportApplicationService(db.Context);
        var period = new ReportPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        var monthly = await service.GenerateMonthlySummaryAsync(period);
        var byManager = await service.GenerateMemberByManagerAsync("A");
        var sakOne = await service.GenerateSakOneAsync(period);

        AssertPdf(monthly);
        AssertPdf(byManager);
        AssertPdf(sakOne);
    }

    [Fact]
    public async Task Official_reports_use_the_canonical_organization_without_company_or_locality_suffixes()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new ReportApplicationService(db.Context);
        var period = new ReportPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        var documents = new[]
        {
            await service.GenerateMemberByManagerAsync("A"),
            await service.GenerateMonthlySummaryAsync(period),
            await service.GenerateSakOneAsync(period),
            await service.GenerateDeathReportAsync(period)
        };

        foreach (var document in documents)
        {
            AssertPdf(document);
            var text = ReportPdfText.Extract(document);

            Assert.NotEmpty(text);
            Assert.Contains("สมาคมฌาปนกิจสงเคราะห์", text);
            Assert.DoesNotContain("สมาคมฌาปนกิจสงเคราะห์ บริษัท GoodApplication", text);
            Assert.DoesNotContain("สมาคมฌาปนกิจสงเคราะห์ อำเภอร้องกวาง จังหวัดแพร่", text);
            Assert.DoesNotContain("GoodApplication", text);
        }
    }

    [Fact]
    public async Task Month_scoped_reports_put_the_buddhist_month_title_directly_under_the_organization()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new ReportApplicationService(db.Context);
        var period = new ReportPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        const string title = "รายงานเดือน 08/2569";
        const string organization = "สมาคมฌาปนกิจสงเคราะห์";

        var monthly = ReportPdfText.Extract(await service.GenerateMonthlySummaryAsync(period));
        var sakOne = ReportPdfText.Extract(await service.GenerateSakOneAsync(period));

        Assert.Contains($"{organization}{title}", monthly);
        Assert.Contains($"{organization}{title}", sakOne);
    }

    [Fact]
    public async Task Monthly_member_report_keeps_member_details_only_once_when_two_beneficiaries_exist()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new ReportApplicationService(db.Context);
        var period = new ReportPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        var text = ReportPdfText.Extract(await service.GenerateMonthlySummaryAsync(period));

        Assert.Equal(1, CountOccurrences(text, "นายสมาชิกหลัก รายงาน"));
        Assert.Equal(1, CountOccurrences(text, "00001"));
        Assert.Equal(1, CountOccurrences(text, "นางผู้รับหนึ่ง รายงาน"));
        Assert.Equal(1, CountOccurrences(text, "นายผู้รับสอง รายงาน"));
    }

    [Fact]
    public async Task All_members_report_lists_non_archived_members_with_one_continuation_row_per_beneficiary()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = new ReportApplicationService(db.Context);

        var document = await service.GenerateAllMembersAsync();
        var text = ReportPdfText.Extract(document);

        Assert.Contains("รายงานสมาชิกทั้งหมด", text);
        Assert.Equal(1, CountOccurrences(text, "นายสมาชิกหลัก รายงาน"));
        Assert.Equal(1, CountOccurrences(text, "00001"));
        Assert.Equal(1, CountOccurrences(text, "นางผู้รับหนึ่ง รายงาน"));
        Assert.Equal(1, CountOccurrences(text, "นายผู้รับสอง รายงาน"));
        Assert.DoesNotContain("สมาชิกเก็บถาวร", text);
    }

    [Fact]
    public async Task Latest_member_application_month_is_reported_as_the_first_day_of_that_month()
    {
        await using var db = await TestDatabase.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        db.Context.Members.Add(new Member { Id = Guid.NewGuid(), RunNo = "00003", Title = "นาย", FirstName = "ล่าสุด", LastName = "รายงาน", GroupNo = "A", District = "ร้องกวาง", Province = "แพร่", ApplicationDate = new DateOnly(2026, 10, 31), ApprovalDate = new DateOnly(2026, 10, 31), CoverageStartDate = new DateOnly(2027, 4, 30), CreatedAtUtc = now, UpdatedAtUtc = now, CreatedBy = "t", UpdatedBy = "t" });
        await db.Context.SaveChangesAsync();
        var service = new ReportApplicationService(db.Context);

        DateOnly? month = await service.GetLatestMemberApplicationMonthAsync();

        Assert.Equal(new DateOnly(2026, 10, 1), month);
    }

    [Fact]
    public async Task Empty_report_data_generates_readable_pdf_documents()
    {
        await using var db = await TestDatabase.CreateEmptyAsync();
        var reports = new ReportApplicationService(db.Context);
        var period = new ReportPeriod(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        var documents = new[]
        {
            await reports.GenerateMemberByManagerAsync("A"),
            await reports.GenerateMonthlySummaryAsync(period),
            await reports.GenerateSakOneAsync(period),
            await reports.GenerateAllMembersAsync()
        };

        foreach (var document in documents)
        {
            AssertPdf(document);
            Assert.Contains("ยังไม่มี", ReportPdfText.Extract(document));
        }
    }

    [Fact]
    public void Death_report_expands_two_beneficiaries_and_blanks_repeated_member_cells()
    {
        var rows = DeathReportRows.Expand(
            new DeathReportSource("00001", "นาย ก ข", new DateOnly(2026, 8, 25), "DC1", 10_001, ["นาง หนึ่ง", "นาย สอง"]));

        Assert.Equal(2, rows.Count);
        Assert.Equal("00001", rows[0].RunNo);
        Assert.Equal("", rows[1].RunNo);
        Assert.Equal("", rows[1].MemberName);
        Assert.Equal("นาย สอง", rows[1].BeneficiaryName);
        Assert.Null(rows[1].AmountSatang);
        Assert.Equal(10_001, rows[0].AmountSatang);
        Assert.True(rows[1].IsContinuation);
    }

    private static void AssertPdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(expected, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += expected.Length;
        }

        return count;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private TestDatabase(SqliteConnection connection, AppDbContext context) { this.connection = connection; Context = context; }
        public AppDbContext Context { get; }
        public static Task<TestDatabase> CreateEmptyAsync() => CreateAsync(seed: false);
        public static async Task<TestDatabase> CreateAsync(bool seed = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            if (seed)
            {
                var now = DateTimeOffset.UtcNow;
                var memberId = Guid.NewGuid();
                context.Members.AddRange(
                    new Member { Id = memberId, RunNo = "00001", Title = "นาย", FirstName = "สมาชิกหลัก", LastName = "รายงาน", GroupNo = "A", District = "ร้องกวาง", Province = "แพร่", ApplicationDate = new DateOnly(2026, 8, 5), ApprovalDate = new DateOnly(2026, 8, 5), CoverageStartDate = new DateOnly(2027, 2, 1), CreatedAtUtc = now, UpdatedAtUtc = now, CreatedBy = "t", UpdatedBy = "t" },
                    new Member { Id = Guid.NewGuid(), RunNo = "00002", Title = "นาย", FirstName = "สมาชิกเก็บถาวร", LastName = "รายงาน", GroupNo = "A", District = "ร้องกวาง", Province = "แพร่", ApplicationDate = new DateOnly(2026, 8, 6), ApprovalDate = new DateOnly(2026, 8, 6), CoverageStartDate = new DateOnly(2027, 2, 2), CreatedAtUtc = now, UpdatedAtUtc = now, CreatedBy = "t", UpdatedBy = "t", ArchivedAtUtc = now, ArchivedBy = "t", ArchiveReason = "test" });
                context.MemberBeneficiaries.AddRange(
                    new MemberBeneficiary { Id = Guid.NewGuid(), MemberId = memberId, SlotNo = 1, Title = "นาง", FirstName = "ผู้รับหนึ่ง", LastName = "รายงาน", CreatedAtUtc = now, UpdatedAtUtc = now, CreatedBy = "t", UpdatedBy = "t" },
                    new MemberBeneficiary { Id = Guid.NewGuid(), MemberId = memberId, SlotNo = 2, Title = "นาย", FirstName = "ผู้รับสอง", LastName = "รายงาน", CreatedAtUtc = now, UpdatedAtUtc = now, CreatedBy = "t", UpdatedBy = "t" });
            }
            await context.SaveChangesAsync(); return new(connection, context);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
