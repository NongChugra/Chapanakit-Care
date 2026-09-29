using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChapanakitCare.Infrastructure.Reports;

/// <summary>Renders the generic presentation contract used by accounting reports and vouchers.</summary>
public static class AccountingDocumentRenderer
{
    private const string ThaiFont = "Leelawadee UI";
    private const string Organization = OfficialReportText.Organization;
    private const string EmptyReportMessage = "ไม่มีรายการในช่วงเวลาที่เลือก";

    static AccountingDocumentRenderer() => QuestPDF.Settings.License = LicenseType.Evaluation;

    public static byte[] GeneratePdf(AccountingDocumentData data)
    {
        Validate(data);

        return Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page);
            // A small explicit top inset keeps a short (for example, empty)
            // report header inside the printable area on every page.
            page.Header().ShowEntire().PaddingTop(5).Element(container => RenderHeader(container, data));
            page.Content().PaddingTop(8).Column(content =>
            {
                content.Item().Element(container => RenderTable(container, data));

                if (data.Notes is { Count: > 0 })
                {
                    content.Item().PaddingTop(10).Column(notes =>
                    {
                        notes.Item().Text(text => text.Span("หมายเหตุ").Bold());
                        foreach (var note in data.Notes)
                            notes.Item().Text(text => text.Span("- " + note).BreakAnywhere());
                    });
                }

                if (data.SignatureLabels is { Count: > 0 })
                {
                    content.Item().PaddingTop(18).ShowEntire().Row(signatures =>
                    {
                        foreach (var label in data.SignatureLabels)
                            signatures.RelativeItem().AlignCenter().Text(text =>
                                text.Span("ลงชื่อ................................................ " + label).BreakAnywhere());
                    });
                }
            });
        })).GeneratePdf();
    }

    public static byte[] GenerateCsv(AccountingDocumentData data)
    {
        Validate(data);

        var builder = new StringBuilder();
        using (var writer = new StringWriter(builder, CultureInfo.InvariantCulture)
               { NewLine = "\r\n" })
        {
            WriteCsvRow(writer, ["องค์กร", Organization], protectAllAsText: true);
            WriteCsvRow(writer, ["สมุดบัญชี", data.BookName], protectAllAsText: true);
            WriteCsvRow(writer, ["ชื่อรายงาน", data.Title], protectAllAsText: true);

            if (BuildPeriodLabel(data) is { Length: > 0 } period)
                WriteCsvRow(writer, ["ช่วงเวลา", period], protectAllAsText: true);
            if (data.AsOfDate is { } asOf)
                WriteCsvRow(writer, ["วันที่อ้างอิง", ThaiReportFormat.Date(asOf)], protectAllAsText: true);
            if (!string.IsNullOrWhiteSpace(data.DocumentNumber))
                WriteCsvRow(writer, ["เลขที่เอกสาร", data.DocumentNumber], protectAllAsText: true);
            if (!string.IsNullOrWhiteSpace(data.Status))
                WriteCsvRow(writer, ["สถานะ", DisplayStatus(data.Status)], protectAllAsText: true);
            if (data.Notes is { Count: > 0 })
                foreach (var note in data.Notes)
                    WriteCsvRow(writer, ["หมายเหตุ", note], protectAllAsText: true);
            if (data.SignatureLabels is { Count: > 0 })
                WriteCsvRow(writer, ["ผู้ลงนาม", .. data.SignatureLabels], protectAllAsText: true);

            WriteCsvRow(writer, data.Columns.Select(column => column.Heading).ToArray(), protectAllAsText: true);
            foreach (var row in data.Rows)
                WriteCsvRow(writer, row, data.Columns);
        }

        var payload = Encoding.UTF8.GetBytes(builder.ToString());
        return [.. Encoding.UTF8.GetPreamble(), .. payload];
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(22);
        page.DefaultTextStyle(style => style.FontFamily(ThaiFont).FontSize(8).LineHeight(1.25f));
        page.Footer().PaddingTop(7).AlignRight().Text(text =>
        {
            text.Span("หน้า ");
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }

    private static void RenderHeader(IContainer container, AccountingDocumentData data)
    {
        container.Column(header =>
        {
            header.Item().AlignCenter().DefaultTextStyle(style => style.FontSize(12))
                .Text(text => text.Span(Organization).Bold().BreakAnywhere());
            header.Item().AlignCenter().DefaultTextStyle(style => style.FontSize(10))
                .Text(text => text.Span("สมุดบัญชี: " + data.BookName).Bold().BreakAnywhere());
            header.Item().AlignCenter().DefaultTextStyle(style => style.FontSize(10))
                .Text(text => text.Span(data.Title).BreakAnywhere());

            if (BuildPeriodLabel(data) is { Length: > 0 } period)
                header.Item().AlignCenter().Text(text => text.Span(period).BreakAnywhere());
            if (data.AsOfDate is { } asOf)
                header.Item().AlignCenter().Text(text => text.Span("ณ วันที่ " + ThaiReportFormat.Date(asOf)));
            if (!string.IsNullOrWhiteSpace(data.DocumentNumber))
                header.Item().AlignRight().Text(text => text.Span("เลขที่เอกสาร " + data.DocumentNumber));
            if (!string.IsNullOrWhiteSpace(data.Status))
            {
                header.Item().PaddingTop(3).AlignCenter().Background("#FFF2CC").Border(0.75f)
                    .PaddingHorizontal(8).PaddingVertical(3)
                    .DefaultTextStyle(style => style.FontSize(11))
                    .Text(text => text.Span(DisplayStatus(data.Status)).Bold().BreakAnywhere());
            }
        });
    }

    private static void RenderTable(IContainer container, AccountingDocumentData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in data.Columns)
                    columns.RelativeColumn(column.RelativeWidth);
            });

            table.Header(header =>
            {
                foreach (var column in data.Columns)
                    StyledCell(header.Cell(), column, header: true)
                        .DefaultTextStyle(style => style.SemiBold())
                        .Text(text => text.Span(column.Heading).BreakAnywhere());
            });

            foreach (var row in data.Rows)
            {
                for (var index = 0; index < data.Columns.Count; index++)
                    StyledCell(table.Cell(), data.Columns[index], header: false)
                        .Text(text => text.Span(row[index]).BreakAnywhere());
            }

            if (data.Rows.Count == 0)
            {
                StyledCell(table.Cell().ColumnSpan((uint)data.Columns.Count), data.Columns[0], header: false)
                    .MinHeight(30)
                    .AlignCenter()
                    .Text(text => text.Span(EmptyReportMessage).BreakAnywhere());
            }
        });
    }

    private static IContainer StyledCell(
        IContainer container,
        AccountingDocumentColumn column,
        bool header)
    {
        var cell = container
            .Border(0.5f)
            .Background(header ? "#EAF2F8" : "#FFFFFF")
            .PaddingHorizontal(3)
            .PaddingVertical(header ? 5 : 3)
            .AlignMiddle();

        return column.Alignment switch
        {
            AccountingDocumentColumnAlignment.Center => cell.AlignCenter(),
            AccountingDocumentColumnAlignment.Right => cell.AlignRight(),
            _ => cell.AlignLeft()
        };
    }

    private static void WriteCsvRow(TextWriter writer, IReadOnlyList<string> values, bool protectAllAsText)
    {
        var output = values.Select(value => EscapeCsv(value, protectAllAsText)).ToArray();
        writer.WriteLine(string.Join(',', output));
    }

    private static void WriteCsvRow(
        TextWriter writer,
        IReadOnlyList<string> values,
        IReadOnlyList<AccountingDocumentColumn> columns)
    {
        var output = values.Select((value, index) =>
            EscapeCsv(value, columns[index].Type == AccountingDocumentColumnType.Text)).ToArray();
        writer.WriteLine(string.Join(',', output));
    }

    private static string EscapeCsv(string value, bool protectFormula)
    {
        if (protectFormula && StartsWithSpreadsheetFormula(value))
            value = "'" + value;

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static bool StartsWithSpreadsheetFormula(string value)
    {
        if (value.Length == 0) return false;

        var firstMeaningful = 0;
        while (firstMeaningful < value.Length && char.IsWhiteSpace(value[firstMeaningful]))
            firstMeaningful++;

        if (firstMeaningful == value.Length) return char.IsControl(value[0]);
        var first = value[firstMeaningful];
        return first is '=' or '+' or '-' or '@' || char.IsControl(value[0]);
    }

    private static string? BuildPeriodLabel(AccountingDocumentData data)
    {
        var period = (data.PeriodFrom, data.PeriodTo) switch
        {
            ({ } from, { } to) => $"ระหว่างวันที่ {ThaiReportFormat.Date(from)} ถึงวันที่ {ThaiReportFormat.Date(to)}",
            ({ } from, null) => $"ตั้งแต่วันที่ {ThaiReportFormat.Date(from)}",
            (null, { } to) => $"ถึงวันที่ {ThaiReportFormat.Date(to)}",
            _ => null
        };
        return period;
    }

    private static string DisplayStatus(string status) => status.Trim().ToUpperInvariant() switch
    {
        "ORIGINAL" => "ORIGINAL / ฉบับจริง",
        "REVERSED" => "REVERSED / กลับรายการ",
        "CANCELLED" or "CANCELED" => "CANCELLED / ยกเลิก",
        "DRAFT" => "DRAFT / ฉบับร่าง",
        _ => status.Trim()
    };

    private static void Validate(AccountingDocumentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data.Title))
            throw new ArgumentException("Document title is required.", nameof(data));
        if (string.IsNullOrWhiteSpace(data.BookName))
            throw new ArgumentException("Book name is required.", nameof(data));
        if (data.Columns is null || data.Columns.Count == 0)
            throw new ArgumentException("At least one document column is required.", nameof(data));
        if (data.Rows is null)
            throw new ArgumentException("Document rows are required.", nameof(data));
        if (data.PeriodFrom is { } from && data.PeriodTo is { } to && from > to)
            throw new ArgumentException("PeriodFrom cannot be later than PeriodTo.", nameof(data));

        for (var columnIndex = 0; columnIndex < data.Columns.Count; columnIndex++)
        {
            var column = data.Columns[columnIndex];
            if (column is null)
                throw new ArgumentException($"Column {columnIndex} is required.", nameof(data));
            if (string.IsNullOrWhiteSpace(column.Heading))
                throw new ArgumentException($"Column {columnIndex} heading is required.", nameof(data));
            if (!Enum.IsDefined(column.Alignment))
                throw new ArgumentException($"Column {columnIndex} alignment is invalid.", nameof(data));
            if (!Enum.IsDefined(column.Type))
                throw new ArgumentException($"Column {columnIndex} type is invalid.", nameof(data));
            if (!float.IsFinite(column.RelativeWidth) || column.RelativeWidth <= 0)
                throw new ArgumentException($"Column {columnIndex} relative width must be positive and finite.", nameof(data));
        }

        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var row = data.Rows[rowIndex];
            if (row is null)
                throw new ArgumentException($"Row {rowIndex} is required.", nameof(data));
            if (row.Count != data.Columns.Count)
                throw new ArgumentException($"Row {rowIndex} has {row.Count} values; expected {data.Columns.Count} columns.", nameof(data));
            for (var cellIndex = 0; cellIndex < row.Count; cellIndex++)
                if (row[cellIndex] is null)
                    throw new ArgumentException($"Row {rowIndex}, column {cellIndex} cannot be null.", nameof(data));
        }

        if (data.Notes is not null)
            for (var noteIndex = 0; noteIndex < data.Notes.Count; noteIndex++)
                if (data.Notes[noteIndex] is null)
                    throw new ArgumentException($"Note {noteIndex} cannot be null.", nameof(data));
        if (data.SignatureLabels is not null)
            for (var labelIndex = 0; labelIndex < data.SignatureLabels.Count; labelIndex++)
                if (data.SignatureLabels[labelIndex] is null)
                    throw new ArgumentException($"Signature label {labelIndex} cannot be null.", nameof(data));
    }
}
