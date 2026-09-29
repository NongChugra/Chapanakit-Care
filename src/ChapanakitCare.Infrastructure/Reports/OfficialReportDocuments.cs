using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChapanakitCare.Infrastructure.Reports;

internal static class OfficialReportDocuments
{
    static OfficialReportDocuments() => QuestPDF.Settings.License = LicenseType.Evaluation;
    private const string ThaiFont = "Leelawadee UI";
    private const string Organization = OfficialReportText.Organization;
    // Enough for all ten digits of an int sequence, including cell padding.
    private const float SequenceColumnWidth = 50;

    public static byte[] MemberByManager(string group, IReadOnlyList<MemberByManagerReportRow> rows, string? leaderName = null) => Document.Create(d => d.Page(page =>
    {
        ConfigurePage(page);
        page.Header().Column(c =>
        {
            c.Item().AlignCenter().Text(Organization).FontSize(12).Bold();
            c.Item().AlignCenter().Text($"รายงานรายชื่อสมาชิกแยกตามผู้ประสานงาน กลุ่ม {group}").FontSize(10);
            c.Item().AlignCenter().Text($"หัวหน้ากลุ่ม: {leaderName ?? "ยังไม่ได้แต่งตั้ง"}   ณ วันที่ {ThaiReportFormat.Date(DateOnly.FromDateTime(DateTime.Today))}");
        });
        page.Content().PaddingTop(8).Element(c => ManagerTable(c, rows));
    })).GeneratePdf();

    public static byte[] AllMembers(IReadOnlyList<MemberByManagerReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        ConfigurePage(page);
        page.Header().Column(c =>
        {
            c.Item().AlignCenter().Text(Organization).FontSize(12).Bold();
            c.Item().AlignCenter().Text("รายงานสมาชิกทั้งหมด").FontSize(10);
            c.Item().AlignCenter().Text($"รายงานรายชื่อสมาชิก ณ วันที่ {ThaiReportFormat.Date(DateOnly.FromDateTime(DateTime.Today))}");
        });
        page.Content().PaddingTop(8).Element(c => ManagerTable(c, rows, "ยังไม่มีสมาชิกในระบบ"));
    })).GeneratePdf();

    public static byte[] Monthly(ReportPeriod period, int opening, int added, int resigned, int deaths, int expelled, int remaining, IReadOnlyList<MonthlyMemberReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        ConfigurePage(page);
        MonthHeader(page, period, "รายงานจำนวนสมาชิกและรายชื่อสมาชิกเข้าใหม่");
        page.Content().PaddingTop(10).Column(c =>
        {
            c.Item().ShowEntire().Element(x => SummaryTable(x, opening, added, resigned, deaths, expelled, remaining));
            c.Item().PaddingTop(20).PaddingBottom(6).Text("รายงานรายชื่อสมาชิกเข้าใหม่").FontSize(10).Bold();
            c.Item().Element(x => MonthlyTable(x, rows));
            c.Item().PaddingTop(12).ShowEntire().Column(signatures =>
            {
                signatures.Item().Text($"รวมสมาชิกใหม่ {added:N0} คน   รับรองว่าถูกต้องตามนี้").FontSize(9);
                signatures.Item().PaddingTop(18).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text("ลงชื่อ................................................ นายกสมาคม");
                    row.RelativeItem().AlignCenter().Text("ลงชื่อ................................................ นายทะเบียน");
                });
            });
        });
    })).GeneratePdf();

    public static byte[] SakOne(ReportPeriod period, IReadOnlyList<SakOneReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        ConfigurePage(page, 7.5f);
        MonthHeader(page, period, "ทะเบียนสมาชิกและการเปลี่ยนแปลง (ประจำเดือน)", "แบบ ส.ฌ.ก.๑");
        page.Content().PaddingTop(8).Element(c => SakOneTable(c, rows));
    })).GeneratePdf();

    private static void ConfigurePage(PageDescriptor page, float fontSize = 8)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(18);
        page.DefaultTextStyle(t => t.FontFamily(ThaiFont).FontSize(fontSize).LineHeight(1.25f));
        page.Footer().PaddingTop(7).AlignRight().Text(t =>
        {
            t.Span("หน้า "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
        });
    }

    private static void MonthHeader(PageDescriptor page, ReportPeriod period, string title, string? form = null) => page.Header().Column(c =>
    {
        c.Item().AlignCenter().Text(Organization).FontSize(12).Bold();
        c.Item().AlignCenter().Text($"รายงานเดือน {ThaiReportFormat.Month(period.From)}").FontSize(10);
        c.Item().AlignCenter().Text(title).FontSize(10);
        if (form is not null) c.Item().AlignRight().Text(form).Bold();
    });

    private static IContainer Cell(IContainer c, bool header = false) => c.Border(0.5f)
        .PaddingHorizontal(2).PaddingVertical(header ? 5 : 3).AlignMiddle();

    private static void MemberColumns(TableColumnsDefinitionDescriptor c)
    {
        c.ConstantColumn(SequenceColumnWidth); c.ConstantColumn(32); c.RelativeColumn(1.1f); c.ConstantColumn(68);
        for (var i = 0; i < 4; i++) c.ConstantColumn(50);
        c.RelativeColumn(1.5f); c.RelativeColumn(1.1f); c.ConstantColumn(48);
    }

    private static void ManagerTable(IContainer container, IReadOnlyList<MemberByManagerReportRow> rows, string emptyMessage = "ยังไม่มีสมาชิกในกลุ่มนี้") => container.Table(table =>
    {
        table.ColumnsDefinition(MemberColumns);
        table.Header(h =>
        {
            foreach (var label in new[] { "ที่", "เลข\nสมาชิก", "ชื่อ นามสกุล", "เลขประจำตัว\nบัตรประชาชน", "วันสมัคร", "วันอนุมัติ", "วันคุ้มครอง", "วันเกิด", "ที่อยู่", "ผู้รับผลประโยชน์", "ความ\nสัมพันธ์" })
                Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold();
        });
        foreach (var member in MemberBlocks(rows, x => x.RunNo))
        {
            // Keep each member and their beneficiary continuation rows on the same page.
            table.Cell().ColumnSpan(11).ShowEntire().Table(block =>
            {
                block.ColumnsDefinition(MemberColumns);
                foreach (var row in member)
                    foreach (var value in new[] { row.SequenceNo, row.RunNo, row.MemberName, row.PersonalIdCard, row.ApplicationDate, row.ApprovalDate, row.CoverageStartDate, row.BirthDate, row.Address, row.BeneficiaryName, row.BeneficiaryRelationship })
                        Cell(block.Cell()).Text(value);
            });
        }
        if (rows.Count == 0) Cell(table.Cell().ColumnSpan(11)).MinHeight(24).AlignCenter().Text(emptyMessage);
    });

    private static void SummaryTable(IContainer container, params int[] values) => container.Table(table =>
    {
        table.ColumnsDefinition(c => { for (var i = 0; i < 6; i++) c.RelativeColumn(); });
        Cell(table.Cell().ColumnSpan(6), true).AlignCenter().Text("จำนวนสมาชิก").FontSize(10).SemiBold();
        foreach (var label in new[] { "ยกมาเดือนก่อน", "เข้าใหม่", "ลาออก", "เสียชีวิต", "ให้ออก", "คงเหลือ" })
            Cell(table.Cell(), true).AlignCenter().Text(label).FontSize(9).SemiBold();
        foreach (var value in values) Cell(table.Cell()).MinHeight(25).AlignCenter().Text(value.ToString("N0")).FontSize(10);
    });

    private static void MonthlyColumns(TableColumnsDefinitionDescriptor c)
    {
        c.ConstantColumn(SequenceColumnWidth); c.RelativeColumn(1.3f); c.ConstantColumn(40); c.ConstantColumn(36);
        for (var i = 0; i < 3; i++) c.ConstantColumn(48);
        c.ConstantColumn(22); c.ConstantColumn(64); c.RelativeColumn(1.6f); c.ConstantColumn(58); c.RelativeColumn(1.2f);
    }

    private static void MonthlyTable(IContainer container, IReadOnlyList<MonthlyMemberReportRow> rows) => container.Table(table =>
    {
        table.ColumnsDefinition(MonthlyColumns);
        table.Header(h =>
        {
            foreach (var label in new[] { "ที่", "ชื่อ นามสกุล", "กลุ่ม", "ทะเบียน", "วันสมัคร", "วันที่\nอนุมัติ", "วันเดือน\nปีเกิด", "อายุ", "เลขที่บัตร", "ที่อยู่", "เบอร์โทรศัพท์", "ผู้รับผลประโยชน์" })
                Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold();
        });
        foreach (var member in MemberBlocks(rows, x => x.RunNo))
        {
            table.Cell().ColumnSpan(12).ShowEntire().Table(block =>
            {
                block.ColumnsDefinition(MonthlyColumns);
                foreach (var row in member)
                    foreach (var value in new[] { row.SequenceNo, row.MemberName, row.GroupNo, row.RunNo, row.ApplicationDate, row.ApprovalDate, row.BirthDate, row.Age, row.PersonalIdCard, row.Address, row.Mobile, row.BeneficiaryName })
                        Cell(block.Cell()).Text(value);
            });
        }
        if (rows.Count == 0) Cell(table.Cell().ColumnSpan(12)).MinHeight(24).AlignCenter().Text("ยังไม่มีสมาชิกเข้าใหม่ในช่วงวันที่เลือก");
    });

    private static void SakColumns(TableColumnsDefinitionDescriptor c)
    {
        c.ConstantColumn(SequenceColumnWidth);
        foreach (var width in new[] { 86, 32, 48, 38, 48, 100, 50, 78, 66, 50, 32, 38, 52, 32, 38 }) c.RelativeColumn(width);
    }

    private static void SakOneTable(IContainer container, IReadOnlyList<SakOneReportRow> rows) => container.Table(table =>
    {
        table.ColumnsDefinition(SakColumns);
        table.Header(h =>
        {
            foreach (var label in new[] { "ที่", "ชื่อ นามสกุล", "เลข\nสมาชิก", "วันที่\nสมัคร", "ประเภท\nสมาชิก", "วันเกิด", "ที่อยู่\nปัจจุบัน", "ชื่อสามี\nภรรยา", "ผู้รับเงินสงเคราะห์\nที่ระบุไว้", "ชื่อผู้จัดการศพ\nที่ระบุไว้" })
                Cell(h.Cell().RowSpan(2), true).AlignCenter().Text(label).SemiBold();
            Cell(h.Cell().ColumnSpan(3), true).AlignCenter().Text("การเปลี่ยนแปลงทั่วไป").SemiBold();
            Cell(h.Cell().ColumnSpan(2), true).AlignCenter().Text("การเปลี่ยนแปลง\nกรณีพ้นสมาชิกภาพ").SemiBold();
            Cell(h.Cell().RowSpan(2), true).AlignCenter().Text("หมาย\nเหตุ").SemiBold();
            foreach (var label in new[] { "ว/ด/ป ที่\nเปลี่ยนแปลง", "เดิม", "เปลี่ยน\nเป็น", "ว/ด/ป\nที่พ้น\nสมาชิกภาพ", "สาเหตุ" })
                Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold();
        });
        foreach (var row in rows)
        {
            table.Cell().ColumnSpan(16).ShowEntire().Table(block =>
            {
                block.ColumnsDefinition(SakColumns);
                foreach (var value in new[] { row.SequenceNo, row.MemberName, row.RunNo, row.ApplicationDate, row.MemberType, row.BirthDate, row.Address, row.Spouse, row.Beneficiary, row.FuneralManager, row.ChangedDate, row.ChangedFrom, row.ChangedTo, row.EndDate, row.EndReason, row.Notes })
                    Cell(block.Cell()).Text(value);
            });
        }
        if (rows.Count == 0) Cell(table.Cell().ColumnSpan(16)).MinHeight(24).AlignCenter().Text("ยังไม่มีสมาชิกเข้าใหม่หรือการเปลี่ยนแปลงในช่วงวันที่เลือก");
    });

    private static IEnumerable<IReadOnlyList<T>> MemberBlocks<T>(IReadOnlyList<T> rows, Func<T, string> runNo)
    {
        var block = new List<T>();
        foreach (var row in rows)
        {
            if (runNo(row).Length > 0 && block.Count > 0)
            {
                yield return block;
                block = [];
            }
            block.Add(row);
        }
        if (block.Count > 0) yield return block;
    }
}
