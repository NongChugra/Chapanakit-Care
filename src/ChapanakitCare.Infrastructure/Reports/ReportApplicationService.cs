using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChapanakitCare.Infrastructure.Reports;

public sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public void Validate()
    {
        if (From > To) throw new ArgumentException("วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด");
    }
}

public sealed record DeathReportSource(string RunNo, string MemberName, DateOnly RecordedDate, string CertificateNo, long TotalBenefitSatang, IReadOnlyList<string> Beneficiaries);
public sealed record DeathReportRow(
    string RunNo,
    string MemberName,
    string RecordedDate,
    string CertificateNo,
    string BeneficiaryName,
    long? AmountSatang,
    bool IsContinuation = false);

public static class DeathReportRows
{
    public static IReadOnlyList<DeathReportRow> Expand(DeathReportSource source)
    {
        if (source.Beneficiaries.Count == 0)
            return [new(source.RunNo, source.MemberName, ThaiDate(source.RecordedDate), source.CertificateNo, "-", source.TotalBenefitSatang)];
        return source.Beneficiaries.Select((name, index) => new DeathReportRow(
            index == 0 ? source.RunNo : string.Empty,
            index == 0 ? source.MemberName : string.Empty,
            index == 0 ? ThaiDate(source.RecordedDate) : string.Empty,
            index == 0 ? source.CertificateNo : string.Empty,
            name,
            index == 0 ? source.TotalBenefitSatang : null,
            IsContinuation: index > 0)).ToArray();
    }

    private static string ThaiDate(DateOnly value) => $"{value:dd/MM}/{value.Year + 543}";
}

public sealed class ReportApplicationService(AppDbContext database)
{
    private const string ThaiFont = "Leelawadee UI";

    public async Task<byte[]> GenerateMonthlySummaryAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var membersBefore = await database.Members.CountAsync(value => value.ApplicationDate < period.From, ct);
        var deathsBefore = await (from death in database.DeathCases
                                  join member in database.Members on death.MemberId equals member.Id
                                  where death.RecordState == "confirmed" && death.RecordedBusinessDate < period.From && member.ApplicationDate < period.From
                                  select death).CountAsync(ct);
        var opening = membersBefore - deathsBefore;
        var added = await database.Members.CountAsync(value => value.ApplicationDate >= period.From && value.ApplicationDate <= period.To, ct);
        var deaths = await database.DeathCases.CountAsync(value => value.RecordedBusinessDate >= period.From && value.RecordedBusinessDate <= period.To && value.RecordState == "confirmed", ct);
        var deathsThrough = await (from death in database.DeathCases
                                   join member in database.Members on death.MemberId equals member.Id
                                   where death.RecordState == "confirmed" && death.RecordedBusinessDate <= period.To && member.ApplicationDate <= period.To
                                   select death).CountAsync(ct);
        var remaining = await database.Members.CountAsync(value => value.ApplicationDate <= period.To, ct) - deathsThrough;
        var deathDetails = await LoadDeathRows(period, ct);

        return Generate("รายงานจำนวนสมาชิกประจำเดือน", period, page =>
        {
            page.Content().Column(column =>
            {
                column.Spacing(14);
                column.Item().Element(container => SummaryTable(container, opening, added, deaths, remaining));
                column.Item().Text("รายละเอียดสมาชิกเสียชีวิต").Bold().FontSize(13);
                column.Item().Element(container => DeathTable(container, deathDetails));
            });
        });
    }

    public async Task<byte[]> GenerateDeathReportAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var rows = await LoadDeathRows(period, ct);
        QuestPDF.Settings.License = LicenseType.Evaluation;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(text => text.FontFamily(ThaiFont).FontSize(9));
            page.Header().PaddingBottom(6).Column(column =>
            {
                column.Item().AlignCenter().Text("สมาคมฌาปนกิจสงเคราะห์ อำเภอร้องกวาง จังหวัดแพร่").Bold().FontSize(14);
                column.Item().AlignCenter().Text("รายงานสมาชิกเสียชีวิต").Bold().FontSize(13);
                column.Item().AlignCenter().Text($"ตั้งแต่วันที่ {ThaiDate(period.From)} ถึงวันที่ {ThaiDate(period.To)}");
            });
            page.Content().Element(container => DeathTable(container, rows));
            page.Footer().AlignRight().Text(text => { text.Span("หน้า "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
        })).GeneratePdf();
    }

    public async Task<byte[]> GenerateSakOneAsync(ReportPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        var members = await database.Members.AsNoTracking()
            .Where(value => value.ApplicationDate >= period.From && value.ApplicationDate <= period.To && value.ArchivedAtUtc == null)
            .OrderBy(value => value.RunNo).ToListAsync(ct);
        var ids = members.Select(value => value.Id).ToArray();
        var beneficiaries = await database.MemberBeneficiaries.AsNoTracking()
            .Where(value => ids.Contains(value.MemberId) && value.IsActive).OrderBy(value => value.SlotNo).ToListAsync(ct);

        var pages = members.Select((member, index) => (Member: member, Index: index + 1)).Chunk(14).ToArray();
        if (pages.Length == 0) pages = [[]];

        QuestPDF.Settings.License = LicenseType.Evaluation;
        return Document.Create(document =>
        {
            foreach (var reportPage in pages)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(text => text.FontFamily(ThaiFont).FontSize(9));
                    page.Content().Column(column =>
                    {
                        column.Item().AlignCenter().Text("สมาคมฌาปนกิจสงเคราะห์ อำเภอร้องกวาง จังหวัดแพร่").Bold().FontSize(14);
                        column.Item().AlignCenter().Text("แบบทะเบียนสมาชิกเข้าใหม่ (ประจำเดือน) แบบ ส.ฌ.ก.๑").Bold().FontSize(13);
                        column.Item().AlignCenter().Text($"ตั้งแต่วันที่ {ThaiDate(period.From)} ถึงวันที่ {ThaiDate(period.To)}");
                        column.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(24); columns.RelativeColumn(1.4f); columns.ConstantColumn(54); columns.ConstantColumn(58); columns.RelativeColumn(); columns.RelativeColumn(1.5f); columns.RelativeColumn(1.2f); columns.RelativeColumn(1.2f); columns.RelativeColumn(); });
                            Header(table, ["ที่", "ชื่อ นามสกุล", "เลขสมาชิก", "วันที่สมัคร", "ประเภทสมาชิก", "วันเกิด", "ที่อยู่ปัจจุบัน", "ผู้รับเงินสงเคราะห์", "หมายเหตุ"]);
                            foreach (var entry in reportPage)
                            {
                                var member = entry.Member;
                                var recipients = string.Join("\n", beneficiaries.Where(value => value.MemberId == member.Id).Select(value => $"{value.Title}{value.FirstName} {value.LastName}"));
                                Row(table, [entry.Index.ToString(), $"{member.Title}{member.FirstName} {member.LastName}", member.RunNo, ThaiDate(member.ApplicationDate), "สามัญ", member.BirthDate is null ? "-" : ThaiDate(member.BirthDate.Value), $"{member.HouseNo} ม.{member.Moo} ต.{member.Subdistrict} อ.{member.District} จ.{member.Province} {member.PostalCode}", recipients, ""]);
                            }
                        });
                    });
                    page.Footer().AlignRight().Text(text => { text.Span("หน้า "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
                });
            }
        }).GeneratePdf();
    }

    private async Task<IReadOnlyList<DeathReportRow>> LoadDeathRows(ReportPeriod period, CancellationToken ct)
    {
        var cases = await (from death in database.DeathCases.AsNoTracking()
                           join member in database.DeathMemberSnapshots.AsNoTracking() on death.Id equals member.DeathCaseId
                           join calculation in database.DeathCalculations.AsNoTracking() on death.Id equals calculation.DeathCaseId
                           where death.RecordedBusinessDate >= period.From && death.RecordedBusinessDate <= period.To && death.RecordState == "confirmed"
                           orderby death.DeathSequenceNo
                           select new { death, member, calculation }).ToListAsync(ct);
        var caseIds = cases.Select(value => value.death.Id).ToArray();
        var recipients = await database.DeathBeneficiarySnapshots.AsNoTracking().Where(value => caseIds.Contains(value.DeathCaseId)).OrderBy(value => value.SlotNo).ToListAsync(ct);
        return cases.SelectMany(value => DeathReportRows.Expand(new DeathReportSource(
            value.member.RunNo,
            $"{value.member.Title}{value.member.FirstName} {value.member.LastName}",
            value.death.RecordedBusinessDate,
            value.death.DeathCertificateNo,
            value.calculation.TotalBenefitSatang,
            recipients.Where(recipient => recipient.DeathCaseId == value.death.Id).Select(recipient => $"{recipient.Title}{recipient.FirstName} {recipient.LastName}").ToArray()))).ToArray();
    }

    private static byte[] Generate(string reportName, ReportPeriod period, Action<PageDescriptor> content)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape()); page.Margin(24); page.DefaultTextStyle(text => text.FontFamily(ThaiFont).FontSize(9));
                page.Header().PaddingBottom(6).Column(header =>
                {
                    header.Item().AlignCenter().Text("สมาคมฌาปนกิจสงเคราะห์ อำเภอร้องกวาง จังหวัดแพร่").Bold().FontSize(14);
                    header.Item().AlignCenter().Text(reportName).Bold().FontSize(13);
                    header.Item().AlignCenter().Text($"ตั้งแต่วันที่ {ThaiDate(period.From)} ถึงวันที่ {ThaiDate(period.To)}");
                });
                content(page);
                page.Footer().AlignRight().Text(text => { text.Span("หน้า "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
            });
        }).GeneratePdf();
    }

    private static void SummaryTable(IContainer container, int opening, int added, int deaths, int remaining)
    {
        container.Table(table => { table.ColumnsDefinition(columns => { for (var i = 0; i < 6; i++) columns.RelativeColumn(); }); Header(table, ["ยกมาเดือนก่อน", "เข้าใหม่", "ลาออก", "เสียชีวิต", "ให้ออก", "คงเหลือ"]); Row(table, [opening.ToString("N0"), added.ToString("N0"), "0", deaths.ToString("N0"), "0", remaining.ToString("N0")]); });
    }

    private static void DeathTable(IContainer container, IReadOnlyList<DeathReportRow> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.ConstantColumn(34); columns.ConstantColumn(58); columns.RelativeColumn(1.4f); columns.ConstantColumn(70); columns.ConstantColumn(82); columns.RelativeColumn(1.3f); columns.ConstantColumn(80); });
            Header(table, ["ที่", "เลขสมาชิก", "ชื่อสมาชิก", "วันที่บันทึก", "ใบมรณะบัตร", "ผู้รับเงินสงเคราะห์", "จำนวนเงิน"]);
            if (rows.Count == 0) Row(table, ["", "", "", "", "", "ยังไม่มีรายการในช่วงวันที่", ""]);
            var itemNo = 0;
            foreach (var row in rows)
            {
                if (!row.IsContinuation) itemNo++;
                Row(table, [
                    row.IsContinuation ? "" : itemNo.ToString(),
                    row.RunNo,
                    row.MemberName,
                    row.RecordedDate,
                    row.CertificateNo,
                    row.BeneficiaryName,
                    row.AmountSatang is null ? "" : $"{row.AmountSatang.Value / 100m:N2}"]);
            }
        });
    }

    private static void Header(TableDescriptor table, IReadOnlyList<string> values)
    {
        table.Header(header =>
        {
            foreach (var value in values)
                header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).AlignCenter().Text(value).Bold();
        });
    }

    private static void Row(TableDescriptor table, IReadOnlyList<string> values)
    {
        foreach (var value in values) table.Cell().Border(0.5f).Padding(4).Text(value ?? string.Empty);
    }

    private static string ThaiDate(DateOnly value) => $"{value:dd/MM}/{value.Year + 543}";
}
