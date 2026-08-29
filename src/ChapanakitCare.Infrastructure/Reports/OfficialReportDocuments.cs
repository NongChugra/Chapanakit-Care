using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChapanakitCare.Infrastructure.Reports;

internal static class OfficialReportDocuments
{
    static OfficialReportDocuments() => QuestPDF.Settings.License = LicenseType.Evaluation;
    private const string ThaiFont = "Leelawadee UI";
    private const string Organization = OfficialReportText.Organization;

    public static byte[] MemberByManager(string group, IReadOnlyList<MemberByManagerReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        Portrait(page);
        page.Header().Column(c => { c.Item().Text($"รายงานสมาชิกแยกตามกลุ่ม หรือแยกตามผู้ประสานงาน/หัวหน้ากลุ่ม {group}").FontSize(12); c.Item().PaddingTop(12).LineHorizontal(0.5f); c.Item().PaddingTop(8).AlignCenter().Text(Organization).FontSize(7); c.Item().AlignCenter().Text("รายงานรายชื่อสมาชิก").FontSize(7); });
        page.Content().PaddingTop(5).Element(c => ManagerTable(c, rows));
        page.Footer().AlignRight().Text(t => { t.Span("หน้า "); t.CurrentPageNumber(); });
    })).GeneratePdf();

    public static byte[] AllMembers(IReadOnlyList<MemberByManagerReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        Portrait(page);
        page.Header().Column(c => { c.Item().AlignCenter().Text(Organization).FontSize(8); c.Item().AlignCenter().Text("รายงานสมาชิกทั้งหมด").FontSize(8); c.Item().PaddingTop(8).LineHorizontal(0.5f); });
        page.Content().PaddingTop(5).Element(c => ManagerTable(c, rows, "ยังไม่มีสมาชิกในระบบ"));
        page.Footer().AlignRight().Text(t => { t.Span("หน้า "); t.CurrentPageNumber(); });
    })).GeneratePdf();

    public static byte[] Monthly(ReportPeriod period, int opening, int added, int resigned, int deaths, int expelled, int remaining, IReadOnlyList<MonthlyMemberReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        Landscape(page);
        page.Header().Column(c => { c.Item().PaddingTop(16).AlignCenter().Text(Organization).FontSize(8); c.Item().AlignCenter().Text($"รายงานเดือน {ThaiReportFormat.Month(period.From)}").FontSize(9); c.Item().AlignCenter().Text("รายงานจำนวนสมาชิก").FontSize(8); c.Item().PaddingTop(3).Element(x => SummaryTable(x, opening, added, resigned, deaths, expelled, remaining)); });
        page.Content().PaddingTop(42).Column(c => { c.Item().AlignCenter().Text(Organization).FontSize(6); c.Item().AlignCenter().Text($"รายงานเดือน {ThaiReportFormat.Month(period.From)}").FontSize(7); c.Item().AlignCenter().Text("รายงานสมาชิกเข้าใหม่").FontSize(6); c.Item().PaddingTop(4).Element(x => MonthlyTable(x, rows)); });
    })).GeneratePdf();

    public static byte[] SakOne(ReportPeriod period, IReadOnlyList<SakOneReportRow> rows) => Document.Create(d => d.Page(page =>
    {
        Landscape(page);
        page.Header().Column(c => { c.Item().PaddingTop(22).AlignCenter().Text(Organization).FontSize(8); c.Item().AlignCenter().Text($"รายงานเดือน {ThaiReportFormat.Month(period.From)}").FontSize(10); c.Item().AlignCenter().Text("แบบทะเบียนสมาชิกเข้าใหม่(ประจำเดือน)").FontSize(8); c.Item().AlignRight().Text("หน้า     1\nแบบ ส.ฌ.ก.๑").Bold().FontSize(8); });
        page.Content().PaddingTop(5).Element(c => SakOneTable(c, rows));
    })).GeneratePdf();

    private static void Portrait(PageDescriptor page) { page.Size(PageSizes.A4); page.Margin(16); page.DefaultTextStyle(t => t.FontFamily(ThaiFont).FontSize(5.6f)); }
    private static void Landscape(PageDescriptor page) { page.Size(PageSizes.A4.Landscape()); page.MarginHorizontal(24); page.MarginVertical(16); page.DefaultTextStyle(t => t.FontFamily(ThaiFont).FontSize(6)); }
    private static IContainer Cell(IContainer c, bool header = false) => c.Border(0.45f).PaddingHorizontal(1.5f).PaddingVertical(header ? 3 : 1.5f).AlignMiddle();

    private static void ManagerTable(IContainer container, IReadOnlyList<MemberByManagerReportRow> rows, string emptyMessage = "ยังไม่มีสมาชิกในกลุ่มนี้") => container.Table(table =>
    {
        table.ColumnsDefinition(c => { c.ConstantColumn(16); c.ConstantColumn(23); c.RelativeColumn(1.1f); c.RelativeColumn(1.1f); c.ConstantColumn(27); c.ConstantColumn(27); c.ConstantColumn(27); c.ConstantColumn(27); c.RelativeColumn(1.7f); c.RelativeColumn(1.2f); });
        table.Header(h => { foreach (var label in new[] { "ที่", "เลข\nสมาชิก", "ชื่อ นามสกุล", "เลขประจำตัว\nประชาชน", "วันสมัคร", "วันอนุมัติ", "วันคุ้มครอง", "วันเกิด", "ที่อยู่", "ผู้รับผลประโยชน์" }) Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold(); });
        foreach (var row in rows) foreach (var value in new[] { row.SequenceNo, row.RunNo, row.MemberName, row.PersonalIdCard, row.ApplicationDate, row.ApprovalDate, row.CoverageStartDate, row.BirthDate, row.Address, row.BeneficiaryName }) Cell(table.Cell()).Text(value);
        if (rows.Count == 0) Cell(table.Cell().ColumnSpan(10)).AlignCenter().Text(emptyMessage);
    });

    private static void SummaryTable(IContainer container, params int[] values) => container.Table(table =>
    {
        table.ColumnsDefinition(c => { for (var i = 0; i < 6; i++) c.RelativeColumn(); });
        table.Header(h => { foreach (var label in new[] { "ยกมาเดือนก่อน", "เข้าใหม่", "ลาออก", "เสียชีวิต", "ให้ออก", "คงเหลือ" }) Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold(); });
        foreach (var value in values) Cell(table.Cell()).AlignCenter().Text(value.ToString("N0"));
    });

    private static void MonthlyTable(IContainer container, IReadOnlyList<MonthlyMemberReportRow> rows) => container.Table(table =>
    {
        table.ColumnsDefinition(c => { c.ConstantColumn(15); c.RelativeColumn(1.25f); c.ConstantColumn(25); c.ConstantColumn(28); c.ConstantColumn(27); c.ConstantColumn(27); c.ConstantColumn(27); c.ConstantColumn(14); c.ConstantColumn(42); c.RelativeColumn(1.8f); c.ConstantColumn(38); c.RelativeColumn(1.2f); });
        table.Header(h => { foreach (var label in new[] { "ที่", "ชื่อ นามสกุล", "กลุ่ม", "ทะเบียน", "วันสมัคร", "วันที่อนุมัติ", "วันเดือนปีเกิด", "อายุ", "เลขที่บัตร", "ที่อยู่", "เบอร์โทรศัพท์", "ผู้รับผลประโยชน์" }) Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold(); });
        foreach (var row in rows) foreach (var value in new[] { row.SequenceNo, row.MemberName, row.GroupNo, row.RunNo, row.ApplicationDate, row.ApprovalDate, row.BirthDate, row.Age, row.PersonalIdCard, row.Address, row.Mobile, row.BeneficiaryName }) Cell(table.Cell()).Text(value);
        if (rows.Count == 0) Cell(table.Cell().ColumnSpan(12)).AlignCenter().Text("ยังไม่มีสมาชิกเข้าใหม่ในช่วงวันที่เลือก");
    });

    private static void SakOneTable(IContainer container, IReadOnlyList<SakOneReportRow> rows) => container.Table(table =>
    {
        table.ColumnsDefinition(c => { c.ConstantColumn(24); c.RelativeColumn(1.25f); c.ConstantColumn(38); c.ConstantColumn(45); c.ConstantColumn(38); c.ConstantColumn(48); c.RelativeColumn(1.1f); c.RelativeColumn(1.1f); c.RelativeColumn(1.1f); c.ConstantColumn(37); c.ConstantColumn(37); c.ConstantColumn(37); c.ConstantColumn(40); c.ConstantColumn(40); c.ConstantColumn(40); });
        table.Header(h => { foreach (var label in new[] { "ที่", "ชื่อ นามสกุล", "เลข\nสมาชิก", "วันที่\nสมัคร", "ประเภท\nสมาชิก", "วันเกิด", "ที่อยู่\nปัจจุบัน", "ผู้รับเงินสงเคราะห์\nที่ระบุไว้", "ผู้จัดการศพ\nที่ระบุไว้" }) Cell(h.Cell().RowSpan(2), true).AlignCenter().Text(label).SemiBold(); Cell(h.Cell().ColumnSpan(3), true).AlignCenter().Text("การเปลี่ยนแปลงทั่วไป").SemiBold(); Cell(h.Cell().ColumnSpan(2), true).AlignCenter().Text("การเปลี่ยนแปลงกรณีพ้นสมาชิก").SemiBold(); Cell(h.Cell().RowSpan(2), true).AlignCenter().Text("หมายเหตุ").SemiBold(); foreach (var label in new[] { "วดป.ที่\nเปลี่ยนแปลง", "เดิม", "เปลี่ยนเป็น", "วดป.ที่พ้น\nสมาชิกภาพ", "สาเหตุ" }) Cell(h.Cell(), true).AlignCenter().Text(label).SemiBold(); });
        foreach (var row in rows) foreach (var value in new[] { row.SequenceNo, row.MemberName, row.RunNo, row.ApplicationDate, row.MemberType, row.BirthDate, row.Address, row.Beneficiary, row.FuneralManager, row.ChangedDate, row.ChangedFrom, row.ChangedTo, row.EndDate, row.EndReason, row.Notes }) Cell(table.Cell()).Text(value);
        if (rows.Count == 0) Cell(table.Cell().ColumnSpan(15)).AlignCenter().Text("ยังไม่มีสมาชิกเข้าใหม่ในช่วงวันที่เลือก");
    });
}
