using System.Text;
using System.Text.RegularExpressions;
using ChapanakitCare.Infrastructure.Reports;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingDocumentTests
{
    [Fact]
    public void Csv_uses_utf8_bom_quotes_text_and_preserves_negative_numeric_amounts()
    {
        var data = Document(
            columns:
            [
                new("วันที่", AccountingDocumentColumnAlignment.Center),
                new("รายการ", AccountingDocumentColumnAlignment.Left, 2),
                new("จำนวนเงิน", AccountingDocumentColumnAlignment.Right, 1, AccountingDocumentColumnType.Numeric),
                new("หมายเหตุ", AccountingDocumentColumnAlignment.Left, 2)
            ],
            rows:
            [
                ["01/09/2569", "รับ,สมาชิก", "-900.00", "=SUM(A1:A2)"],
                ["02/09/2569", "คืน\"เงิน", "125.50", "ปกติ\r\nต่อเนื่อง"]
            ]);

        var bytes = AccountingDocumentRenderer.GenerateCsv(data);
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Contains("\"สมุดบัญชี\",\"บัญชีสวัสดิการสมาชิก\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"01/09/2569\",\"รับ,สมาชิก\",\"-900.00\",\"'=SUM(A1:A2)\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"คืน\"\"เงิน\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"ปกติ\r\nต่อเนื่อง\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"'-900.00\"", csv, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ORIGINAL", "ฉบับจริง")]
    [InlineData("REVERSED", "กลับรายการ")]
    [InlineData("CANCELLED", "ยกเลิก")]
    public void Csv_keeps_audit_status_token_and_thai_meaning(string status, string thaiMeaning)
    {
        var bytes = AccountingDocumentRenderer.GenerateCsv(Document(status: status));
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Contains($"\"สถานะ\",\"{status} / {thaiMeaning}\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_contains_book_period_status_document_number_and_hand_derived_values()
    {
        var data = Document(
            title: "ทะเบียนรับเงินประจำวัน",
            bookName: "บัญชีสวัสดิการสมาชิก",
            periodFrom: new DateOnly(2026, 9, 1),
            periodTo: new DateOnly(2026, 9, 30),
            columns:
            [
                new("วันที่", AccountingDocumentColumnAlignment.Center),
                new("รายการ", AccountingDocumentColumnAlignment.Left, 3),
                new("จำนวนเงิน (บาท)", AccountingDocumentColumnAlignment.Right, 2, AccountingDocumentColumnType.Numeric)
            ],
            rows: [["01/09/2569", "รับเงินสงเคราะห์ล่วงหน้า", "-900.00"]],
            notes: ["เอกสารนี้สรุปเฉพาะบัญชีสวัสดิการสมาชิก"],
            documentNumber: "RV-0001",
            status: "ORIGINAL",
            signatureLabels: ["ผู้จัดทำ", "ผู้ตรวจสอบ"]);

        var pdf = AccountingDocumentRenderer.GeneratePdf(data);
        var text = ReportPdfText.Extract(pdf);
        SaveArtifact("voucher-original", pdf, text);

        Assert.Contains("สมาคมฌาปนกิจสงเคราะห์", text, StringComparison.Ordinal);
        Assert.Contains("บัญชีสวัสดิการสมาชิก", text, StringComparison.Ordinal);
        // QuestPDF's embedded Thai glyph map exposes sara-am as its two glyphs;
        // assert the stable title prefix while visual QA checks the composed word.
        Assert.Contains("ทะเบียนรับเงิน", text, StringComparison.Ordinal);
        Assert.Contains("01/09/2569", text, StringComparison.Ordinal);
        Assert.Contains("30/09/2569", text, StringComparison.Ordinal);
        Assert.Contains("RV-0001", text, StringComparison.Ordinal);
        Assert.Contains("ORIGINAL", text, StringComparison.Ordinal);
        Assert.Contains("ฉบับจริง", text, StringComparison.Ordinal);
        Assert.Contains("รับเงินสงเคราะห์ล่วงหน้า", text, StringComparison.Ordinal);
        Assert.Contains("-900.00", text, StringComparison.Ordinal);
        Assert.Contains("ผู้จัด", text, StringComparison.Ordinal);
        Assert.Contains("ผู้ตรวจ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_empty_report_keeps_context_and_shows_no_data_message()
    {
        var data = Document(
            title: "สมุดรายวันทั่วไป",
            bookName: "บัญชีสมาคม",
            asOfDate: new DateOnly(2026, 9, 30),
            columns: [new("เลขที่", AccountingDocumentColumnAlignment.Center), new("รายการ")],
            rows: []);

        var pdf = AccountingDocumentRenderer.GeneratePdf(data);
        var text = ReportPdfText.Extract(pdf);
        SaveArtifact("empty-report", pdf, text);

        Assert.Contains("บัญชีสมาคม", text, StringComparison.Ordinal);
        Assert.Contains("สมุดรายวันทั่วไป", text, StringComparison.Ordinal);
        Assert.Contains("ณ วันที่ 30/09/2569", text, StringComparison.Ordinal);
        Assert.Contains("ไม่มีรายการ", text, StringComparison.Ordinal);
        Assert.Contains("หน้า", text, StringComparison.Ordinal);
        Assert.Matches(@"/Type /Page\b", Encoding.Latin1.GetString(pdf));
    }

    [Fact]
    public void Pdf_long_table_wraps_and_repeats_headings_across_pages()
    {
        var rows = Enumerable.Range(1, 120)
            .Select(index => (IReadOnlyList<string>)[
                index.ToString(),
                $"สมาชิก {index} พร้อมรายละเอียดรายการที่ยาวพอให้ตัดบรรทัดเพื่อป้องกันการตัดข้อความออกจากเอกสาร",
                $"-{index * 900}.00"])
            .ToArray();
        var data = Document(
            title: "บัญชีรายการเคลื่อนไหว",
            bookName: "บัญชีสวัสดิการสมาชิก",
            columns:
            [
                new("ลำดับ", AccountingDocumentColumnAlignment.Right),
                new("รายละเอียด", AccountingDocumentColumnAlignment.Left, 5),
                new("จำนวนเงิน", AccountingDocumentColumnAlignment.Right, 2, AccountingDocumentColumnType.Numeric)
            ],
            rows: rows);

        var pdf = AccountingDocumentRenderer.GeneratePdf(data);
        var raw = Encoding.Latin1.GetString(pdf);
        var text = ReportPdfText.Extract(pdf);
        SaveArtifact("long-report", pdf, text);
        var pageCount = Regex.Matches(raw, @"/Type /Page\b").Count;

        Assert.True(pageCount > 1, $"Expected a multipage document, got {pageCount} page(s).");
        Assert.Contains("สมาชิก 120", text, StringComparison.Ordinal);
        Assert.Contains("-108000.00", text, StringComparison.Ordinal);
        Assert.True(Count(text, "รายละเอียด") >= 2, "The table heading should repeat on later pages.");
        for (var page = 1; page <= pageCount; page++)
            Assert.Contains($"หน้า{page}/{pageCount}", Regex.Replace(text, @"\s", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_rejects_a_row_that_does_not_match_the_column_count()
    {
        var data = Document(
            columns: [new("เลขที่"), new("รายการ")],
            rows: [["1"]]);

        var exception = Assert.Throws<ArgumentException>(() => AccountingDocumentRenderer.GenerateCsv(data));

        Assert.Contains("column", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renderer_requires_an_explicit_book_name_for_every_document()
    {
        var data = Document(bookName: " ");

        var exception = Assert.Throws<ArgumentException>(() => AccountingDocumentRenderer.GenerateCsv(data));

        Assert.Contains("Book name", exception.Message, StringComparison.Ordinal);
    }

    private static AccountingDocumentData Document(
        string title = "รายงานบัญชี",
        string bookName = "บัญชีสวัสดิการสมาชิก",
        DateOnly? periodFrom = null,
        DateOnly? periodTo = null,
        DateOnly? asOfDate = null,
        IReadOnlyList<AccountingDocumentColumn>? columns = null,
        IReadOnlyList<IReadOnlyList<string>>? rows = null,
        IReadOnlyList<string>? notes = null,
        string? documentNumber = null,
        string? status = null,
        IReadOnlyList<string>? signatureLabels = null) => new(
            title,
            bookName,
            periodFrom,
            periodTo,
            asOfDate,
            columns ?? [new("รายการ")],
            rows ?? [["ทดสอบ"]],
            notes,
            documentNumber,
            status,
            signatureLabels);

    private static int Count(string value, string token) =>
        Regex.Matches(value, Regex.Escape(token), RegexOptions.CultureInvariant).Count;

    private static void SaveArtifact(string name, byte[] pdf, string text)
    {
        var directory = Environment.GetEnvironmentVariable("ACCOUNTING_DOCUMENT_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".pdf"), pdf);
        File.WriteAllText(Path.Combine(directory, name + ".txt"), text, Encoding.UTF8);
    }
}
