using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChapanakitCare.Infrastructure.Reports;

public sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public void Validate() { if (From > To) throw new ArgumentException("วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด"); }
}

public sealed record DeathReportSource(string RunNo, string MemberName, DateOnly RecordedDate, string CertificateNo, long TotalBenefitSatang, IReadOnlyList<string> Beneficiaries);
public sealed record DeathReportRow(string RunNo, string MemberName, string RecordedDate, string CertificateNo, string BeneficiaryName, long? AmountSatang, bool IsContinuation = false);

public static class DeathReportRows
{
    public static IReadOnlyList<DeathReportRow> Expand(DeathReportSource source) => source.Beneficiaries.Count == 0
        ? [new(source.RunNo, source.MemberName, ThaiDate(source.RecordedDate), source.CertificateNo, "-", source.TotalBenefitSatang)]
        : source.Beneficiaries.Select((name, i) => new DeathReportRow(i == 0 ? source.RunNo : "", i == 0 ? source.MemberName : "", i == 0 ? ThaiDate(source.RecordedDate) : "", i == 0 ? source.CertificateNo : "", name, i == 0 ? source.TotalBenefitSatang : null, i > 0)).ToArray();
    private static string ThaiDate(DateOnly value) => ThaiBuddhistDate.Format(value);
}

public sealed class ReportApplicationService(AppDbContext database)
{
    public async Task<IReadOnlyList<string>> GetManagerGroupsAsync(CancellationToken ct = default) => await database.Members.AsNoTracking().Where(x => x.GroupNo != null && x.GroupNo != "").Select(x => x.GroupNo!).Distinct().OrderBy(x => x).ToListAsync(ct);
    public async Task<DateOnly?> GetLatestMemberApplicationMonthAsync(CancellationToken ct = default)
    {
        var latest = await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null).Select(x => (DateOnly?)x.ApplicationDate).MaxAsync(ct);
        return latest is null ? null : new DateOnly(latest.Value.Year, latest.Value.Month, 1);
    }

    public async Task<byte[]> GenerateAllMembersAsync(CancellationToken ct = default)
    {
        var members = await LoadMembersAsync(_ => true, ct);
        return OfficialReportDocuments.AllMembers(ExpandMemberRows(members));
    }

    public async Task<byte[]> GenerateMemberByManagerAsync(string groupNo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupNo)) throw new ArgumentException("ต้องเลือกกลุ่มสมาชิก", nameof(groupNo));
        var members = await LoadMembersAsync(x => x.GroupNo == groupNo, ct);
        var rows = ExpandMemberRows(members);
        QuestPDF.Settings.License = LicenseType.Evaluation;
        return OfficialReportDocuments.MemberByManager(groupNo, rows);
    }
    public async Task<byte[]> GenerateMonthlySummaryAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var opening = await database.Members.CountAsync(x => x.ArchivedAtUtc == null && x.ApplicationDate < period.From, ct);
        var added = await database.Members.CountAsync(x => x.ArchivedAtUtc == null && x.ApplicationDate >= period.From && x.ApplicationDate <= period.To, ct);
        var resigned = await database.MemberStatusEvents.CountAsync(x => x.ToStatus == "resigned" && x.EffectiveDate >= period.From && x.EffectiveDate <= period.To, ct);
        var deaths = await database.DeathCases.CountAsync(x => x.RecordState == "confirmed" && x.RecordedBusinessDate >= period.From && x.RecordedBusinessDate <= period.To, ct);
        var members = await LoadMembersAsync(x => x.ApplicationDate >= period.From && x.ApplicationDate <= period.To, ct);
        var rows = members.SelectMany((x, i) => ExpandMonthly(x, i + 1, period.To)).ToArray();
        return OfficialReportDocuments.Monthly(period, opening, added, resigned, deaths, 0, Math.Max(0, opening + added - resigned - deaths), rows);
    }
    public Task<byte[]> GenerateDeathReportAsync(ReportPeriod period, CancellationToken ct = default) => GenerateAsync(period, "รายงานสมาชิกเสียชีวิต", ct);
    public async Task<byte[]> GenerateSakOneAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var members = await LoadMembersAsync(x => x.ApplicationDate >= period.From && x.ApplicationDate <= period.To, ct);
        var rows = members.Select((x, i) => new SakOneReportRow((i + 1).ToString(), Name(x.Member), x.Member.RunNo, ThaiDate(x.Member.ApplicationDate), "-", ThaiDate(x.Member.BirthDate), Address(x.Member), x.Beneficiaries.Count == 0 ? "-" : string.Join("\n", x.Beneficiaries.Select(Name)), "-", "-", "-", "-", "-", "-", "-")).ToArray();
        return OfficialReportDocuments.SakOne(period, rows);
    }

    private async Task<byte[]> GenerateAsync(ReportPeriod period, string title, CancellationToken ct)
    {
        period.Validate();
        _ = await database.Members.AsNoTracking().CountAsync(ct);
        QuestPDF.Settings.License = LicenseType.Evaluation;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape()); page.Margin(24);
            page.DefaultTextStyle(text => text.FontFamily("Leelawadee UI").FontSize(10));
            page.Header().AlignCenter().Column(column => { column.Item().Text(OfficialReportText.Organization).Bold(); column.Item().Text(title).Bold(); column.Item().Text($"ตั้งแต่วันที่ {ThaiDate(period.From)} ถึงวันที่ {ThaiDate(period.To)}"); });
            page.Content().PaddingTop(20).Text("ไม่มีรายการในช่วงวันที่");
            page.Footer().AlignRight().Text(text => { text.Span("หน้า "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
        })).GeneratePdf();
    }
    private static string ThaiDate(DateOnly value) => ThaiBuddhistDate.Format(value);
    private static string ThaiDate(DateOnly? value) => value is null ? "-" : ThaiDate(value.Value);
    private async Task<IReadOnlyList<(Member Member, IReadOnlyList<MemberBeneficiary> Beneficiaries)>> LoadMembersAsync(System.Linq.Expressions.Expression<Func<Member, bool>> filter, CancellationToken ct)
    {
        var members = await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null).Where(filter).OrderBy(x => x.RunNo).ToListAsync(ct);
        var ids = members.Select(x => x.Id).ToArray();
        var beneficiaries = await database.MemberBeneficiaries.AsNoTracking().Where(x => ids.Contains(x.MemberId) && x.IsActive).OrderBy(x => x.SlotNo).ToListAsync(ct);
        return members.Select(x => (x, (IReadOnlyList<MemberBeneficiary>)beneficiaries.Where(b => b.MemberId == x.Id).ToArray())).ToArray();
    }
    private static IReadOnlyList<MemberByManagerReportRow> ExpandMemberRows(IReadOnlyList<(Member Member, IReadOnlyList<MemberBeneficiary> Beneficiaries)> members) => members
        .SelectMany((x, i) => MemberByManagerReportRows.Expand(new MemberByManagerReportSource(x.Member.RunNo, x.Member.Title, x.Member.FirstName, x.Member.LastName, x.Member.PersonalIdCard, x.Member.ApplicationDate, x.Member.ApprovalDate, x.Member.CoverageStartDate, x.Member.BirthDate, Address(x.Member), x.Beneficiaries.Select(Name).ToArray()), i + 1))
        .ToArray();
    private static IReadOnlyList<MonthlyMemberReportRow> ExpandMonthly((Member Member, IReadOnlyList<MemberBeneficiary> Beneficiaries) item, int sequence, DateOnly asOf)
    {
        var beneficiaries = item.Beneficiaries.Count == 0 ? ["-"] : item.Beneficiaries.Select(Name).ToArray();
        return beneficiaries.Select((name, i) => new MonthlyMemberReportRow(i == 0 ? sequence.ToString() : "", i == 0 ? Name(item.Member) : "", i == 0 ? item.Member.GroupNo ?? "-" : "", i == 0 ? item.Member.RunNo : "", i == 0 ? ThaiDate(item.Member.ApplicationDate) : "", i == 0 ? ThaiDate(item.Member.ApprovalDate) : "", i == 0 ? ThaiDate(item.Member.BirthDate) : "", i == 0 ? ThaiReportFormat.Age(item.Member.BirthDate, asOf) : "", i == 0 ? item.Member.PersonalIdCard ?? "-" : "", i == 0 ? Address(item.Member) : "", i == 0 ? item.Member.Mobile ?? "-" : "", name)).ToArray();
    }
    private static string Name(Member value) => $"{value.Title}{value.FirstName} {value.LastName}".Trim();
    private static string Name(MemberBeneficiary value) => $"{value.Title}{value.FirstName} {value.LastName}".Trim();
    private static string Address(Member value) => string.Join(" ", new[] { value.HouseNo, value.Under, value.Moo is null ? null : $"ม.{value.Moo}", value.Subdistrict is null ? null : $"ต.{value.Subdistrict}", $"อ.{value.District}", $"จ.{value.Province}", value.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
