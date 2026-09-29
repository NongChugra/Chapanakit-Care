using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.AccountingReports;

public sealed record WelfareCollectionBalanceRow(
    Guid CollectionId,
    Guid MemberId,
    string MemberRunNo,
    string MemberName,
    string GroupNo,
    string CycleKey,
    DateOnly BusinessDate,
    DateOnly DueDate,
    long RequestedSatang,
    long PaidSatang,
    long OutstandingSatang);

public sealed record WelfareCollectionBalanceReport(
    DateOnly AsOfDate,
    string? CycleKey,
    string? GroupNo,
    IReadOnlyList<WelfareCollectionBalanceRow> Rows,
    long TotalRequestedSatang,
    long TotalPaidSatang,
    long TotalOutstandingSatang);

public sealed class CollectionReportService
{
    private readonly AppDbContext database;

    public CollectionReportService(AppDbContext database)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<WelfareCollectionBalanceReport> GetBalancesAsync(
        DateOnly asOfDate,
        string? cycleKey = null,
        string? groupNo = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCycleKey = NormalizeFilter(cycleKey);
        var normalizedGroupNo = NormalizeFilter(groupNo);
        var collectionsQuery = database.Set<WelfareCollection>()
            .AsNoTracking()
            .Where(collection => collection.BusinessDate <= asOfDate);
        if (normalizedCycleKey is not null)
            collectionsQuery = collectionsQuery.Where(collection => collection.CycleKey == normalizedCycleKey);
        if (normalizedGroupNo is not null)
            collectionsQuery = collectionsQuery.Where(collection => collection.GroupNo == normalizedGroupNo);

        var collections = await collectionsQuery
            .OrderBy(collection => collection.GroupNo)
            .ThenBy(collection => collection.MemberRunNo)
            .ThenBy(collection => collection.CycleKey)
            .ThenBy(collection => collection.Id)
            .ToListAsync(cancellationToken);
        var collectionIds = collections.Select(collection => collection.Id).ToArray();

        var paidByCollection = collectionIds.Length == 0
            ? new Dictionary<Guid, long>()
            : await (
                    from line in database.AccountingJournalLines.AsNoTracking()
                    join journal in database.AccountingJournals.AsNoTracking()
                        on line.JournalId equals journal.Id
                    join book in database.AccountingBooks.AsNoTracking()
                        on journal.BookId equals book.Id
                    where line.CollectionRequestId != null && collectionIds.Contains(line.CollectionRequestId.Value)
                          && journal.BusinessDate <= asOfDate
                          && line.BookId == journal.BookId
                          && book.Code == AccountingBookCode.Welfare
                    group line by line.CollectionRequestId into grouped
                    select new
                    {
                        CollectionId = grouped.Key!.Value,
                        PaidSatang = grouped.Sum(line => line.CreditSatang - line.DebitSatang)
                    })
                .ToDictionaryAsync(row => row.CollectionId, row => row.PaidSatang, cancellationToken);

        var rows = new List<WelfareCollectionBalanceRow>(collections.Count);
        long totalRequested = 0;
        long totalPaid = 0;
        long totalOutstanding = 0;
        foreach (var collection in collections)
        {
            var paid = paidByCollection.GetValueOrDefault(collection.Id);
            var outstanding = checked(collection.AmountSatang - paid);
            rows.Add(new WelfareCollectionBalanceRow(
                collection.Id,
                collection.MemberId,
                collection.MemberRunNo,
                collection.MemberName,
                collection.GroupNo,
                collection.CycleKey,
                collection.BusinessDate,
                collection.DueDate,
                collection.AmountSatang,
                paid,
                outstanding));
            totalRequested = checked(totalRequested + collection.AmountSatang);
            totalPaid = checked(totalPaid + paid);
            totalOutstanding = checked(totalOutstanding + outstanding);
        }

        return new WelfareCollectionBalanceReport(
            asOfDate,
            normalizedCycleKey,
            normalizedGroupNo,
            rows,
            totalRequested,
            totalPaid,
            totalOutstanding);
    }

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class CollectionReportDocuments
{
    private const string WelfareBookName = "บัญชีสวัสดิการสมาชิก";

    public static AccountingDocumentData ToCollectionSheet(WelfareCollectionBalanceReport report)
    {
        Validate(report);
        return CreateDocument(
            "ใบสรุปรอบเรียกเก็บเงินสงเคราะห์",
            report,
            report.Rows,
            IncludePaidColumns,
            [
                "ยอดเรียกเก็บรวม " + Money(report.TotalRequestedSatang) + " บาท",
                "รับเงินจริงและจัดสรรแล้ว " + Money(report.TotalPaidSatang) + " บาท",
                "ยอดค้างรวม " + Money(report.TotalOutstandingSatang) + " บาท",
                FilterNote(report)
            ]);
    }

    public static AccountingDocumentData ToOutstandingDocument(WelfareCollectionBalanceReport report)
    {
        Validate(report);
        var rows = report.Rows.Where(row => row.OutstandingSatang > 0).ToList();
        return CreateDocument(
            "รายงานยอดค้างชำระรอบเรียกเก็บ",
            report,
            rows,
            IncludePaidColumns,
            [
                "ยอดค้างชำระ " + Money(Sum(rows.Select(row => row.OutstandingSatang))) + " บาท",
                FilterNote(report)
            ]);
    }

    public static AccountingDocumentData ToReminderDocument(WelfareCollectionBalanceReport report)
    {
        Validate(report);
        var rows = report.Rows
            .Where(row => row.OutstandingSatang > 0 && row.DueDate <= report.AsOfDate)
            .ToList();
        return CreateDocument(
            "รายการแจ้งเตือนยอดค้างชำระ",
            report,
            rows,
            ReminderColumns,
            [
                "ยอดค้างที่ถึงกำหนด " + Money(Sum(rows.Select(row => row.OutstandingSatang))) + " บาท",
                "เอกสารนี้ใช้พิมพ์ตรวจสอบหรือแจ้งเตือนด้วยตนเองเท่านั้น ระบบไม่ส่งอัตโนมัติและไม่เปลี่ยนสถานะสมาชิก",
                FilterNote(report)
            ]);
    }

    private static readonly IReadOnlyList<AccountingDocumentColumn> IncludePaidColumns =
    [
        TextColumn("กลุ่ม", AccountingDocumentColumnAlignment.Center),
        TextColumn("รอบเรียกเก็บ", AccountingDocumentColumnAlignment.Center),
        TextColumn("เลขสมาชิก", AccountingDocumentColumnAlignment.Center),
        TextColumn("ชื่อสมาชิก", width: 2.5f),
        TextColumn("วันครบกำหนด", AccountingDocumentColumnAlignment.Center),
        MoneyColumn("เรียกเก็บ (บาท)"),
        MoneyColumn("รับแล้ว (บาท)"),
        MoneyColumn("ค้าง (บาท)")
    ];

    private static readonly IReadOnlyList<AccountingDocumentColumn> ReminderColumns =
    [
        TextColumn("กลุ่ม", AccountingDocumentColumnAlignment.Center),
        TextColumn("รอบเรียกเก็บ", AccountingDocumentColumnAlignment.Center),
        TextColumn("เลขสมาชิก", AccountingDocumentColumnAlignment.Center),
        TextColumn("ชื่อสมาชิก", width: 3),
        TextColumn("วันครบกำหนด", AccountingDocumentColumnAlignment.Center),
        MoneyColumn("ยอดค้าง (บาท)")
    ];

    private static AccountingDocumentData CreateDocument(
        string title,
        WelfareCollectionBalanceReport report,
        IReadOnlyList<WelfareCollectionBalanceRow> rows,
        IReadOnlyList<AccountingDocumentColumn> columns,
        IReadOnlyList<string> notes)
    {
        var isReminder = ReferenceEquals(columns, ReminderColumns);
        var documentRows = rows.Select(row => isReminder
            ? Row(
                row.GroupNo,
                row.CycleKey,
                row.MemberRunNo,
                row.MemberName,
                ThaiReportFormat.Date(row.DueDate),
                Money(row.OutstandingSatang))
            : Row(
                row.GroupNo,
                row.CycleKey,
                row.MemberRunNo,
                row.MemberName,
                ThaiReportFormat.Date(row.DueDate),
                Money(row.RequestedSatang),
                Money(row.PaidSatang),
                Money(row.OutstandingSatang)))
            .ToList();
        return new AccountingDocumentData(
            title,
            WelfareBookName,
            null,
            null,
            report.AsOfDate,
            columns,
            documentRows,
            notes);
    }

    private static void Validate(WelfareCollectionBalanceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Rows);
    }

    private static AccountingDocumentColumn TextColumn(
        string heading,
        AccountingDocumentColumnAlignment alignment = AccountingDocumentColumnAlignment.Left,
        float width = 1) =>
        new(heading, alignment, width, AccountingDocumentColumnType.Text);

    private static AccountingDocumentColumn MoneyColumn(string heading, float width = 1) =>
        new(heading, AccountingDocumentColumnAlignment.Right, width, AccountingDocumentColumnType.Numeric);

    private static IReadOnlyList<string> Row(params string[] values) => values;

    private static string Money(long satang) => FinanceMoney.Format(satang);

    private static long Sum(IEnumerable<long> values) =>
        values.Aggregate(0L, (total, value) => checked(total + value));

    private static string FilterNote(WelfareCollectionBalanceReport report)
    {
        var filters = new List<string>();
        if (report.CycleKey is { } cycleKey) filters.Add("รอบ " + cycleKey);
        if (report.GroupNo is { } groupNo) filters.Add("กลุ่ม " + groupNo);
        return filters.Count == 0
            ? "แสดงทุกรอบและทุกกลุ่มที่เกิดไม่เกินวันที่อ้างอิง"
            : "ตัวกรอง: " + string.Join(" · ", filters);
    }
}
