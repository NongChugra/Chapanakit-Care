using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointFourReportTests
{
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
            context.Members.Add(new Member { Id = Guid.NewGuid(), RunNo = "00001", Title = "นาย", FirstName = "ทดสอบ", LastName = "รายงาน", GroupNo = "A", District = "ร้องกวาง", Province = "แพร่", ApplicationDate = new DateOnly(2026, 8, 5), ApprovalDate = new DateOnly(2026, 8, 5), CoverageStartDate = new DateOnly(2027, 2, 1), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "t", UpdatedBy = "t" });
            await context.SaveChangesAsync(); return new(connection, context);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
