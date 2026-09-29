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
    public async Task<IReadOnlyList<ManagerGroupOption>> GetManagerGroupOptionsAsync(CancellationToken ct = default)
    {
        var groups = await GetManagerGroupsAsync(ct);
        var leaders = await (from position in database.CoordinatorPositions.AsNoTracking()
                             join member in database.Members.AsNoTracking() on position.MemberId equals member.Id
                             where position.RoleCode == "group_leader" && position.GroupNo == member.GroupNo
                                 && member.ArchivedAtUtc == null && member.Status == MemberStatus.Normal
                             select new { GroupNo = position.GroupNo!, Member = member }).ToListAsync(ct);
        return groups.Select(group => new ManagerGroupOption(group,
            leaders.FirstOrDefault(leader => leader.GroupNo == group) is { } leader ? Name(leader.Member) : null)).ToArray();
    }
    public async Task<IReadOnlyList<string>> GetManagerGroupsAsync(CancellationToken ct = default) => await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null && x.GroupNo != null && x.GroupNo != "").Select(x => x.GroupNo!).Distinct().OrderBy(x => x).ToListAsync(ct);
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
        var group = (await GetManagerGroupOptionsAsync(ct)).FirstOrDefault(x => x.GroupNo == groupNo);
        return OfficialReportDocuments.MemberByManager(groupNo, rows, group?.LeaderName);
    }
    public async Task<byte[]> GenerateMonthlySummaryAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var admitted = await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null && x.ApplicationDate <= period.To).Select(x => new { x.Id, x.ApplicationDate }).ToListAsync(ct);
        var exits = await LoadExitsAsync(period.To, ct);
        var opening = admitted.Count(x => x.ApplicationDate < period.From && (!exits.TryGetValue(x.Id, out var exit) || exit.Date >= period.From));
        var added = admitted.Count(x => x.ApplicationDate >= period.From);
        var periodExits = admitted.Where(x => exits.TryGetValue(x.Id, out var exit) && exit.Date >= period.From).Select(x => exits[x.Id]).ToArray();
        var resigned = periodExits.Count(x => x.Status == "resigned");
        var deaths = periodExits.Count(x => x.Status == "deceased");
        var expelled = periodExits.Count(x => x.Status == "expelled");
        var members = await LoadMembersAsync(x => x.ApplicationDate >= period.From && x.ApplicationDate <= period.To, ct);
        var rows = members.SelectMany((x, i) => ExpandMonthly(x, i + 1, period.To)).ToArray();
        return OfficialReportDocuments.Monthly(period, opening, added, resigned, deaths, expelled, opening + added - resigned - deaths - expelled, rows);
    }
    public Task<byte[]> GenerateDeathReportAsync(ReportPeriod period, CancellationToken ct = default) => GenerateAsync(period, "รายงานสมาชิกเสียชีวิต", ct);
    public async Task<byte[]> GenerateSakOneAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var fromUtc = LocalDayStartUtc(period.From);
        var throughUtc = LocalDayStartUtc(period.To.AddDays(1));
        var audits = await database.AuditEvents.FromSqlInterpolated($"SELECT * FROM audit_events WHERE action = 'member.updated' AND member_id IS NOT NULL AND julianday(occurred_at_utc) >= julianday({fromUtc:O}) AND julianday(occurred_at_utc) < julianday({throughUtc:O})")
            .AsNoTracking().ToListAsync(ct);
        var auditIds = audits.Select(x => x.Id).ToArray();
        var changes = await database.AuditFieldChanges.AsNoTracking()
            .Where(x => auditIds.Contains(x.AuditEventId)).ToListAsync(ct);
        var changedMemberIds = audits.Where(x => changes.Any(change => change.AuditEventId == x.Id))
            .Select(x => x.MemberId!.Value).Distinct().ToArray();
        var members = await LoadMembersAsync(x =>
            (x.ApplicationDate >= period.From && x.ApplicationDate <= period.To) || changedMemberIds.Contains(x.Id), ct);
        var exits = await LoadExitsAsync(period.To, ct);
        var rows = new List<SakOneReportRow>();
        foreach (var (x, i) in members.Select((member, index) => (member, index)))
        {
            exits.TryGetValue(x.Member.Id, out var exit);
            var memberChanges = (from audit in audits
                                 where audit.MemberId == x.Member.Id
                                 from change in changes
                                 where change.AuditEventId == audit.Id
                                 orderby audit.OccurredAtUtc, change.FieldName
                                 select (audit, change)).ToArray();
            var baseRow = new SakOneReportRow((i + 1).ToString(), Name(x.Member), x.Member.RunNo,
                ThaiDate(x.Member.ApplicationDate), "-", ThaiDate(x.Member.BirthDate), Address(x.Member),
                x.Beneficiaries.Count == 0 ? "-" : string.Join("\n", x.Beneficiaries.Select(Name)),
                "-", "-", "-", "-", exit is null ? "-" : ThaiDate(exit.Date),
                exit?.Status switch { "resigned" => "ลาออก", "deceased" => "เสียชีวิต", "expelled" => "ให้ออก", _ => "-" }, "-");
            if (memberChanges.Length == 0) { rows.Add(baseRow); continue; }
            foreach (var (audit, change) in memberChanges)
            {
                rows.Add(baseRow with
                {
                    ChangedDate = ThaiDate(DateOnly.FromDateTime(audit.OccurredAtUtc.ToLocalTime().DateTime)),
                    ChangedFrom = $"{ChangeLabel(change.FieldName)}: {change.OldValueDisplay ?? "–"}",
                    ChangedTo = change.NewValueDisplay ?? "–"
                });
                baseRow = baseRow with { SequenceNo = "", MemberName = "", RunNo = "", ApplicationDate = "",
                    MemberType = "", BirthDate = "", Address = "", Beneficiary = "", FuneralManager = "",
                    EndDate = "", EndReason = "", Notes = "", Spouse = "" };
            }
        }
        return OfficialReportDocuments.SakOne(period, rows);
    }

    private static DateTimeOffset LocalDayStartUtc(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
    }

    private static string ChangeLabel(string field)
    {
        var beneficiary = field.Split('.', 2);
        if (beneficiary.Length == 2 && beneficiary[0].StartsWith("Beneficiary", StringComparison.Ordinal)
            && int.TryParse(beneficiary[0]["Beneficiary".Length..], out var slot))
            return $"ผู้รับเงิน {slot}: {ChangeLabel(beneficiary[1])}";
        return field switch
        {
        "Title" => "คำนำหน้า", "FirstName" => "ชื่อ", "LastName" => "นามสกุล",
        "Gender" => "เพศ", "PersonalIdCard" => "เลขประชาชน", "BirthDate" => "วันเกิด",
        "Relationship" => "ความสัมพันธ์",
        "Mobile" => "โทรศัพท์", "HouseNo" => "บ้านเลขที่", "Moo" => "หมู่ที่",
        "Under" => "หมู่บ้าน", "Subdistrict" => "ตำบล", "District" => "อำเภอ",
        "Province" => "จังหวัด", "PostalCode" => "รหัสไปรษณีย์", "GroupNo" => "กลุ่มสมาชิก",
        "ApplicationDate" => "วันที่สมัคร", "ApprovalDate" => "วันอนุมัติ",
        _ => field
        };
    }

    private sealed record MembershipExit(Guid MemberId, DateOnly Date, string Status);

    private async Task<IReadOnlyDictionary<Guid, MembershipExit>> LoadExitsAsync(DateOnly through, CancellationToken ct)
    {
        var events = await database.MemberStatusEvents.AsNoTracking()
            .Where(x => x.EffectiveDate <= through && (x.ToStatus == "resigned" || x.ToStatus == "deceased" || x.ToStatus == "expelled"))
            .Select(x => new MembershipExit(x.MemberId, x.EffectiveDate, x.ToStatus)).ToListAsync(ct);
        var deaths = await database.DeathCases.AsNoTracking()
            .Where(x => x.RecordState == "confirmed" && x.RecordedBusinessDate <= through)
            .Select(x => new MembershipExit(x.MemberId, x.RecordedBusinessDate, "deceased")).ToListAsync(ct);
        // Membership has no reinstatement flow. Collapse the event and confirmed case
        // into one exit so a death never reduces a report total twice.
        return events.Concat(deaths).GroupBy(x => x.MemberId).ToDictionary(x => x.Key, x => x.OrderBy(exit => exit.Date).First());
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
        .SelectMany((x, i) => MemberByManagerReportRows.Expand(new MemberByManagerReportSource(x.Member.RunNo, x.Member.Title, x.Member.FirstName, x.Member.LastName, x.Member.PersonalIdCard, x.Member.ApplicationDate, x.Member.ApprovalDate, x.Member.CoverageStartDate, x.Member.BirthDate, Address(x.Member), x.Beneficiaries.Select(Name).ToArray(), x.Beneficiaries.Select(b => b.Relationship ?? "-").ToArray()), i + 1))
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
