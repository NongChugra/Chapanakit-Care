namespace ChapanakitCare.Infrastructure.Reports;

/// <summary>How a table cell is aligned in a rendered accounting document.</summary>
public enum AccountingDocumentColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>Whether a CSV cell is trusted as a number or treated as text.</summary>
public enum AccountingDocumentColumnType
{
    Text,
    Numeric
}

/// <summary>Presentation metadata for one accounting report column.</summary>
public sealed record AccountingDocumentColumn(
    string Heading,
    AccountingDocumentColumnAlignment Alignment = AccountingDocumentColumnAlignment.Left,
    float RelativeWidth = 1,
    AccountingDocumentColumnType Type = AccountingDocumentColumnType.Text);

/// <summary>
/// A complete, book-specific snapshot supplied by an accounting query to the
/// document renderer. It contains no database references or accounting logic.
/// </summary>
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
