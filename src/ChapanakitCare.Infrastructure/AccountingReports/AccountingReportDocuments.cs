using System.Globalization;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Reports;

namespace ChapanakitCare.Infrastructure.AccountingReports;

/// <summary>
/// Converts immutable accounting report snapshots into the generic printable and
/// CSV-safe document contract. Monetary values remain derived from integer
/// satang and are formatted as exact two-decimal baht only at this boundary.
/// </summary>
public static class AccountingReportDocuments
{
    private const string WelfareBookName = "บัญชีสวัสดิการสมาชิก";

    public static AccountingDocumentData ToDocumentData(AccountingTrialBalanceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Rows);
        return new AccountingDocumentData(
            "งบทดลอง",
            report.BookName,
            null,
            null,
            report.AsOfDate,
            [
                TextColumn("รหัสบัญชี", AccountingDocumentColumnAlignment.Center),
                TextColumn("ชื่อบัญชี", width: 3),
                MoneyColumn("เดบิต (บาท)"),
                MoneyColumn("เครดิต (บาท)")
            ],
            report.Rows.Select(row => Row(
                row.AccountCode,
                row.AccountName,
                Money(row.DebitBalanceSatang),
                Money(row.CreditBalanceSatang))).ToList(),
            [
                "รวมเดบิต " + Money(report.TotalDebitSatang) + " บาท",
                "รวมเครดิต " + Money(report.TotalCreditSatang) + " บาท"
            ]);
    }

    public static AccountingDocumentData ToDocumentData(AccountingJournalReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Entries);

        var rows = new List<IReadOnlyList<string>>();
        foreach (var entry in report.Entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Lines);
            foreach (var line in entry.Lines)
            {
                rows.Add(Row(
                    ThaiReportFormat.Date(entry.BusinessDate),
                    entry.JournalNumber,
                    entry.VoucherType,
                    entry.Description,
                    line.AccountCode,
                    line.AccountName,
                    Money(line.DebitSatang),
                    Money(line.CreditSatang),
                    entry.IsReversal ? "กลับรายการ" : "ปกติ"));
            }
        }

        return new AccountingDocumentData(
            "สมุดรายวันทั่วไป",
            report.BookName,
            report.Period.From,
            report.Period.To,
            null,
            [
                TextColumn("วันที่", AccountingDocumentColumnAlignment.Center),
                TextColumn("เลขที่ใบสำคัญ", AccountingDocumentColumnAlignment.Center),
                TextColumn("ประเภท"),
                TextColumn("รายละเอียด", width: 2),
                TextColumn("รหัสบัญชี", AccountingDocumentColumnAlignment.Center),
                TextColumn("ชื่อบัญชี", width: 2),
                MoneyColumn("เดบิต (บาท)"),
                MoneyColumn("เครดิต (บาท)"),
                TextColumn("สถานะ", AccountingDocumentColumnAlignment.Center)
            ],
            rows,
            [
                "รวมเดบิต " + Money(report.TotalDebitSatang) + " บาท",
                "รวมเครดิต " + Money(report.TotalCreditSatang) + " บาท"
            ]);
    }

    public static AccountingDocumentData ToDocumentData(AccountingGeneralLedgerReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Entries);
        return new AccountingDocumentData(
            "บัญชีแยกประเภท: " + report.AccountCode + " " + report.AccountName,
            report.BookName,
            report.Period.From,
            report.Period.To,
            null,
            [
                TextColumn("วันที่", AccountingDocumentColumnAlignment.Center),
                TextColumn("เลขที่ใบสำคัญ", AccountingDocumentColumnAlignment.Center),
                TextColumn("ประเภท"),
                TextColumn("รายละเอียด", width: 2),
                MoneyColumn("เดบิต (บาท)"),
                MoneyColumn("เครดิต (บาท)"),
                MoneyColumn("ยอดเดบิตคงเหลือ (บาท)", width: 1.5f),
                MoneyColumn("ยอดเครดิตคงเหลือ (บาท)", width: 1.5f)
            ],
            report.Entries.Select(entry => Row(
                ThaiReportFormat.Date(entry.BusinessDate),
                entry.JournalNumber,
                entry.VoucherType,
                entry.Description,
                Money(entry.DebitSatang),
                Money(entry.CreditSatang),
                Money(entry.RunningDebitBalanceSatang),
                Money(entry.RunningCreditBalanceSatang))).ToList(),
            [
                BalanceNote("ยอดยกมา", report.OpeningDebitBalanceSatang, report.OpeningCreditBalanceSatang),
                "ยอดเคลื่อนไหวเดบิต " + Money(report.MovementDebitSatang) + " บาท",
                "ยอดเคลื่อนไหวเครดิต " + Money(report.MovementCreditSatang) + " บาท",
                BalanceNote("ยอดคงเหลือ", report.ClosingDebitBalanceSatang, report.ClosingCreditBalanceSatang)
            ]);
    }

    public static AccountingDocumentData ToDocumentData(AccountingIncomeExpenseReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Rows);
        return new AccountingDocumentData(
            "รายงานรายได้และค่าใช้จ่าย",
            report.BookName,
            report.Period.From,
            report.Period.To,
            null,
            [
                TextColumn("รหัสบัญชี", AccountingDocumentColumnAlignment.Center),
                TextColumn("ชื่อบัญชี", width: 3),
                TextColumn("ประเภท", AccountingDocumentColumnAlignment.Center),
                MoneyColumn("เดบิต (บาท)"),
                MoneyColumn("เครดิต (บาท)"),
                MoneyColumn("สุทธิ (บาท)")
            ],
            report.Rows.Select(row => Row(
                row.AccountCode,
                row.AccountName,
                AccountTypeName(row.AccountType),
                Money(row.DebitSatang),
                Money(row.CreditSatang),
                Money(row.NetSatang))).ToList(),
            [
                "รวมรายได้ " + Money(report.TotalIncomeSatang) + " บาท",
                "รวมค่าใช้จ่าย " + Money(report.TotalExpenseSatang) + " บาท",
                "ผลการดำเนินงานสุทธิ " + Money(report.NetResultSatang) + " บาท"
            ]);
    }

    public static AccountingDocumentData ToDocumentData(AccountingFinancialPositionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Assets);
        ArgumentNullException.ThrowIfNull(report.Liabilities);
        ArgumentNullException.ThrowIfNull(report.Equity);

        var rows = new List<IReadOnlyList<string>>();
        AddFinancialPositionRows(rows, "สินทรัพย์", report.Assets);
        AddFinancialPositionRows(rows, "หนี้สิน", report.Liabilities);
        AddFinancialPositionRows(rows, "ส่วนทุน", report.Equity);
        rows.Add(Row("ส่วนทุน", string.Empty, "ผลการดำเนินงานงวดปัจจุบัน", Money(report.CurrentResultSatang)));

        return new AccountingDocumentData(
            "งบแสดงฐานะการเงิน",
            report.BookName,
            null,
            null,
            report.AsOfDate,
            [
                TextColumn("หมวด", AccountingDocumentColumnAlignment.Center),
                TextColumn("รหัสบัญชี", AccountingDocumentColumnAlignment.Center),
                TextColumn("ชื่อบัญชี", width: 3),
                MoneyColumn("จำนวนเงิน (บาท)")
            ],
            rows,
            [
                "รวมสินทรัพย์ " + Money(report.TotalAssetsSatang) + " บาท",
                "รวมหนี้สินและส่วนทุน " + Money(report.TotalLiabilitiesAndEquitySatang) + " บาท",
                report.IsBalanced ? "งบสมดุล" : "งบไม่สมดุล"
            ]);
    }

    public static AccountingDocumentData ToDocumentData(AccountingMemberBalancesReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Rows);
        return new AccountingDocumentData(
            "รายงานยอดเงินสงเคราะห์ล่วงหน้ารายสมาชิก",
            WelfareBookName,
            null,
            null,
            report.AsOfDate,
            [
                TextColumn("เลขสมาชิก", AccountingDocumentColumnAlignment.Center),
                TextColumn("ชื่อสมาชิก", width: 3),
                MoneyColumn("เงินล่วงหน้า (บาท)"),
                MoneyColumn("เงินขาด (บาท)"),
                MoneyColumn("สุทธิ (บาท)")
            ],
            report.Rows.Select(row => Row(
                row.MemberRunNo,
                row.MemberName,
                Money(row.AdvanceSatang),
                Money(row.ShortfallSatang),
                Money(row.NetAdvanceSatang))).ToList(),
            [
                "ยอดเงินสงเคราะห์ล่วงหน้ารวม " + Money(report.TotalAdvanceSatang) + " บาท",
                "ยอดเงินขาดรวม " + Money(report.TotalShortfallSatang) + " บาท",
                "ยอดสุทธิ " + Money(report.NetAdvanceSatang) + " บาท"
            ]);
    }

    public static AccountingDocumentData ToDocumentData(AccountingBeneficiaryUnpaidReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Rows);
        return new AccountingDocumentData(
            "รายงานเงินสงเคราะห์ค้างจ่ายผู้รับผลประโยชน์",
            WelfareBookName,
            null,
            null,
            report.AsOfDate,
            [
                TextColumn("รหัสกรณีเสียชีวิต", AccountingDocumentColumnAlignment.Center, 1.5f),
                TextColumn("ลำดับ", AccountingDocumentColumnAlignment.Center),
                TextColumn("ผู้รับผลประโยชน์", width: 3),
                MoneyColumn("ตั้งหนี้ (บาท)"),
                MoneyColumn("จ่ายแล้ว (บาท)"),
                MoneyColumn("ค้างจ่าย (บาท)")
            ],
            report.Rows.Select(row => Row(
                row.DeathCaseId.ToString(),
                row.BeneficiarySlotNo.ToString(CultureInfo.InvariantCulture),
                row.BeneficiaryName,
                Money(row.PayableSatang),
                Money(row.PaidSatang),
                Money(row.UnpaidSatang))).ToList(),
            [
                "ตั้งหนี้เงินสงเคราะห์รวม " + Money(report.TotalPayableSatang) + " บาท",
                "จ่ายเงินสงเคราะห์แล้วรวม " + Money(report.TotalPaidSatang) + " บาท",
                "เงินสงเคราะห์ค้างจ่ายรวม " + Money(report.TotalUnpaidSatang) + " บาท"
            ]);
    }

    public static byte[] GeneratePdf(AccountingTrialBalanceReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GeneratePdf(AccountingJournalReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GeneratePdf(AccountingGeneralLedgerReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GeneratePdf(AccountingIncomeExpenseReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GeneratePdf(AccountingFinancialPositionReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GeneratePdf(AccountingMemberBalancesReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GeneratePdf(AccountingBeneficiaryUnpaidReport report) =>
        AccountingDocumentRenderer.GeneratePdf(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingTrialBalanceReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingJournalReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingGeneralLedgerReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingIncomeExpenseReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingFinancialPositionReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingMemberBalancesReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    public static byte[] GenerateCsv(AccountingBeneficiaryUnpaidReport report) =>
        AccountingDocumentRenderer.GenerateCsv(ToDocumentData(report));

    private static void AddFinancialPositionRows(
        ICollection<IReadOnlyList<string>> rows,
        string category,
        IReadOnlyList<AccountingFinancialPositionLine> lines)
    {
        foreach (var line in lines)
            rows.Add(Row(category, line.AccountCode, line.AccountName, Money(line.AmountSatang)));
    }

    private static AccountingDocumentColumn TextColumn(
        string heading,
        AccountingDocumentColumnAlignment alignment = AccountingDocumentColumnAlignment.Left,
        float width = 1) =>
        new(heading, alignment, width, AccountingDocumentColumnType.Text);

    private static AccountingDocumentColumn MoneyColumn(string heading, float width = 1) =>
        new(heading, AccountingDocumentColumnAlignment.Right, width, AccountingDocumentColumnType.Numeric);

    private static IReadOnlyList<string> Row(params string[] values) => values;

    private static string Money(long satang) =>
        (satang / 100m).ToString("N2", CultureInfo.InvariantCulture);

    private static string BalanceNote(string prefix, long debitSatang, long creditSatang) =>
        debitSatang > 0 || creditSatang == 0
            ? prefix + "เดบิต " + Money(debitSatang) + " บาท"
            : prefix + "เครดิต " + Money(creditSatang) + " บาท";

    private static string AccountTypeName(AccountingAccountType accountType) => accountType switch
    {
        AccountingAccountType.Income => "รายได้",
        AccountingAccountType.Expense => "ค่าใช้จ่าย",
        _ => throw new ArgumentException("รายงานรายได้และค่าใช้จ่ายต้องมีเฉพาะบัญชีรายได้หรือค่าใช้จ่าย", nameof(accountType))
    };
}
