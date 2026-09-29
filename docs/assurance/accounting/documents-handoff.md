# Accounting document renderer handoff

Status: contract published before implementation. This file is the integration
boundary for the report query and UI workers. The renderer owns presentation;
queries own accounting meaning, filtering, totals and preformatted values.

## Public contract

The implementation lives in `src/ChapanakitCare.Infrastructure/Reports/` and
does not read the database. Callers supply a complete, book-specific snapshot:

```csharp
public enum AccountingDocumentColumnAlignment
{
    Left,
    Center,
    Right
}

public enum AccountingDocumentColumnType
{
    Text,
    Numeric
}

public sealed record AccountingDocumentColumn(
    string Heading,
    AccountingDocumentColumnAlignment Alignment = AccountingDocumentColumnAlignment.Left,
    float RelativeWidth = 1,
    AccountingDocumentColumnType Type = AccountingDocumentColumnType.Text);

public sealed record AccountingDocumentData(
    string Title,
    string BookName,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    DateOnly? AsOfDate,
    IReadOnlyList<AccountingDocumentColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<string>? Notes = null,
    string? DocumentNumber = null,
    string? Status = null,
    IReadOnlyList<string>? SignatureLabels = null);

public static class AccountingDocumentRenderer
{
    public static byte[] GeneratePdf(AccountingDocumentData data);
    public static byte[] GenerateCsv(AccountingDocumentData data);
}
```

`Rows` must contain one value for each column. Values are already presentation
strings, including complete negative amounts; no value is inferred from the
current database or from a member counter. A numeric column is declared with
`AccountingDocumentColumnType.Numeric`. The renderer preserves numeric values
as supplied and applies spreadsheet formula-injection protection only to text
columns. Callers should format amounts with integer satang conversion before
passing them to this layer.

## Presentation rules

- `BookName` is required and appears in the PDF header and the CSV metadata
  section. Welfare and association books therefore produce separate documents;
  the renderer has no combined-book default.
- `PeriodFrom`/`PeriodTo` render as a Buddhist Era date range. `AsOfDate`
  renders as an `ณ วันที่` line. The model stores all dates as Gregorian
  `DateOnly`; conversion occurs only here.
- PDF pages are A4 landscape by default, use the existing `Leelawadee UI`
  QuestPDF convention, repeat organization/book/title/period and table column
  headings, wrap long text, show a useful empty-report row, and number every
  page.
- `DocumentNumber` and `Status` are optional. Known statuses are rendered with
  an English audit token and Thai meaning, for example `ORIGINAL / ฉบับจริง`,
  `REVERSED / กลับรายการ`, `CANCELLED / ยกเลิก`, and `DRAFT / ฉบับร่าง`.
  Unknown status text is retained verbatim. This generic header supports
  receipt, payment, journal and reversal vouchers without a second template.
- Signature labels are presentation lines only. They do not authenticate or
  approve a journal.
- CSV is UTF-8 with a BOM and RFC 4180-style quoting. Fields containing commas,
  quotes, CR or LF are quoted and quotes are doubled. Text values beginning
  with `=`, `+`, `-`, `@` or a control whitespace are prefixed with an
  apostrophe. Numeric values, including negative amounts, remain unchanged.

## Caller example

```csharp
var document = new AccountingDocumentData(
    Title: "ทะเบียนรับเงินประจำวัน",
    BookName: "บัญชีสวัสดิการสมาชิก",
    PeriodFrom: new DateOnly(2026, 9, 1),
    PeriodTo: new DateOnly(2026, 9, 30),
    AsOfDate: null,
    Columns:
    [
        new("วันที่", AccountingDocumentColumnAlignment.Center, 1),
        new("รายการ", AccountingDocumentColumnAlignment.Left, 3),
        new("จำนวนเงิน (บาท)", AccountingDocumentColumnAlignment.Right, 2,
            AccountingDocumentColumnType.Numeric)
    ],
    Rows:
    [
        ["01/09/2569", "รับเงินสงเคราะห์ล่วงหน้า", "-900.00"]
    ],
    Notes: ["เอกสารนี้สรุปเฉพาะบัญชีสวัสดิการสมาชิก"],
    DocumentNumber: "RV-0001",
    Status: "ORIGINAL",
    SignatureLabels: ["ผู้จัดทำ", "ผู้ตรวจสอบ"]);

var pdf = AccountingDocumentRenderer.GeneratePdf(document);
var csv = AccountingDocumentRenderer.GenerateCsv(document);
```

The query layer should create one document for `welfare` and one for
`association`, selecting the appropriate book name and source journal rows.
Fee accrual/remittance may be linked at the operation layer, but the renderer
must not merge the two books or recognize income a second time.

## Validation boundary

The renderer rejects a blank title/book, no columns, invalid relative widths,
invalid enum values, null notes/signatures/row values and rows whose length
differs from the column count. It does not validate accounting balance,
account codes, journal status, amount rounding or whether a report is
authorized; those decisions belong to the posting/query layer. Empty `Rows`
is valid and renders a report with headings and an explicit no-data message.

Focused behavior tests and generated QA samples belong under
`docs/assurance/accounting/document-samples/`. The lead should run the focused
document tests after ledger integration, then inspect the rendered PNGs before
claiming the final accounting boundary.
