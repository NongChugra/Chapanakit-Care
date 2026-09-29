using System.Text;
using System.Text.RegularExpressions;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class ReportLayoutTests
{
    private static readonly ReportPeriod Period = new(new(2026, 8, 1), new(2026, 8, 31));

    [Theory]
    [InlineData("all", "1000")]
    [InlineData("group", "1000")]
    [InlineData("monthly", "1000")]
    [InlineData("sak", "1000")]
    [InlineData("all", "2147483647")]
    [InlineData("group", "2147483647")]
    [InlineData("monthly", "2147483647")]
    [InlineData("sak", "2147483647")]
    public void Sequence_numbers_remain_whole_through_the_integer_count_range(string kind, string sequence)
    {
        // Exercise the real layout with a late row, without allocating billions of members.
        // Reflection keeps this test-only seam out of the production public API.
        var document = typeof(ReportApplicationService).Assembly.GetType("ChapanakitCare.Infrastructure.Reports.OfficialReportDocuments")!;
        MemberByManagerReportRow[] memberRows = [new(sequence, "00001", "นาย ทดสอบ", "-", "01/08/2569", "01/08/2569", "01/02/2570", "01/01/2523", "1", "-", "-")];
        MonthlyMemberReportRow[] monthlyRows = [new(sequence, "นาย ทดสอบ", "0101", "00001", "01/08/2569", "01/08/2569", "01/01/2523", "46", "-", "1", "-", "-")];
        SakOneReportRow[] sakRows = [new(sequence, "นาย ทดสอบ", "00001", "01/08/2569", "-", "01/01/2523", "1", "-", "-", "-", "-", "-", "-", "-", "-")];
        var (method, parameters) = kind switch
        {
            "all" => ("AllMembers", new object?[] { memberRows }),
            "group" => ("MemberByManager", new object?[] { "0101", memberRows, null }),
            "monthly" => ("Monthly", new object?[] { Period, 0, 0, 0, 0, 0, 0, monthlyRows }),
            _ => ("SakOne", new object?[] { Period, sakRows })
        };
        var bytes = Assert.IsType<byte[]>(document.GetMethod(method)!.Invoke(null, parameters));
        SaveArtifact($"{kind}-sequence-{sequence}", bytes);
        Assert.Contains(sequence, ReportPdfText.Extract(bytes, separateTextPositions: true), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("all", "ความสัมพันธ์")]
    [InlineData("group", "ความสัมพันธ์")]
    [InlineData("monthly", "รับรองว่าถูกต้องตามนี้")]
    [InlineData("sak", "ชื่อสามีภรรยา")]
    public async Task Reference_columns_and_certification_are_not_omitted(string kind, string required)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var bytes = await Generate(new(db), kind);
        var text = ReportPdfText.Extract(bytes);
        Assert.Contains(required, text);
        Assert.Contains("ยังไม่มี", text);
        Assert.DoesNotContain("GoodApplication", text);
        if (kind == "monthly")
        {
            Assert.Contains("รวมสมาชิกใหม่", text);
            Assert.Contains("นายทะเบียน", text);
            Assert.Contains("นายกสมาคม", text);
        }
        SaveArtifact(kind + "-empty", bytes);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("group")]
    [InlineData("monthly")]
    [InlineData("sak")]
    public async Task Multipage_reports_keep_every_member_and_beneficiary_and_number_every_page(string kind)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        for (var i = 1; i <= 80; i++)
        {
            var member = TestData.Member();
            member.Id = Guid.NewGuid();
            member.RunNo = i.ToString("D5");
            member.Title = "นาย";
            member.FirstName = "กิตติพัฒน์" + i.ToString("D3");
            member.LastName = "วัฒนพิพัฒนกุล";
            member.GroupNo = "0101";
            member.PersonalIdCard = "1234567890" + i.ToString("D3");
            member.Mobile = "0812345678";
            member.BirthDate = new(1980, 12, 31);
            member.ApplicationDate = new(2026, 8, 15);
            member.ApprovalDate = new(2026, 8, 16);
            member.CoverageStartDate = new(2027, 2, 12);
            member.HouseNo = "123/456";
            member.Under = "บ้านหนองประดู่";
            member.Moo = "12";
            member.Subdistrict = "แม่ยางร้อง";
            member.PostalCode = "54140";
            db.Members.Add(member);
            for (var slot = 1; slot <= 2; slot++)
            {
                var beneficiary = TestData.Beneficiary(slot);
                beneficiary.Id = Guid.NewGuid();
                beneficiary.MemberId = member.Id;
                beneficiary.FirstName = $"ผู้รับ{i:D3}คน{slot}";
                beneficiary.LastName = "วัฒนพิพัฒนกุล";
                beneficiary.Relationship = slot == 1 ? "บุตร" : "คู่สมรส";
                db.MemberBeneficiaries.Add(beneficiary);
            }
        }
        await db.SaveChangesAsync();
        var bytes = await Generate(new(db), kind);
        var text = ReportPdfText.Extract(bytes);
        var pageCount = Regex.Matches(Encoding.Latin1.GetString(bytes), @"/Type /Page\b").Count;
        var compactText = Regex.Replace(text, @"\s", "");
        Assert.True(pageCount > 1);
        for (var i = 1; i <= 80; i++)
        {
            Assert.Single(Regex.Matches(compactText, $"นายกิตติพัฒน์{i:D3}วัฒนพิพัฒนกุล"));
            Assert.Single(Regex.Matches(compactText, $"ผู้รับ{i:D3}คน1วัฒนพิพัฒนกุล"));
            Assert.Single(Regex.Matches(compactText, $"ผู้รับ{i:D3}คน2วัฒนพิพัฒนกุล"));
        }
        for (var page = 1; page <= pageCount; page++)
            Assert.Contains($"หน้า{page}/{pageCount}", compactText);
        var mediaBox = Regex.Match(Encoding.Latin1.GetString(bytes), @"/MediaBox\s*\[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.True(mediaBox.Success);
        Assert.True(double.Parse(mediaBox.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) > double.Parse(mediaBox.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        SaveArtifact(kind + "-multipage", bytes);
    }

    private static Task<byte[]> Generate(ReportApplicationService service, string kind) => kind switch
    {
        "all" => service.GenerateAllMembersAsync(),
        "group" => service.GenerateMemberByManagerAsync("0101"),
        "monthly" => service.GenerateMonthlySummaryAsync(Period),
        _ => service.GenerateSakOneAsync(Period)
    };

    private static void SaveArtifact(string name, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("REPORT_LAYOUT_ARTIFACTS") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".pdf"), bytes);
    }
}
