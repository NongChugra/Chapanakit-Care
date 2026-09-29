using ChapanakitCare.Domain.Entities;

namespace ChapanakitCare.Infrastructure.AccountingReports;

public sealed record AccountingReportPeriod(DateOnly From, DateOnly To)
{
    public void Validate()
    {
        if (From > To)
            throw new ArgumentException("วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด", nameof(From));
    }
}

public sealed record AccountingTrialBalanceRow(
    string AccountCode,
    string AccountName,
    AccountingAccountType AccountType,
    AccountingNormalBalance NormalBalance,
    long DebitBalanceSatang,
    long CreditBalanceSatang);

public sealed record AccountingTrialBalanceReport(
    AccountingBookCode BookCode,
    string BookName,
    DateOnly AsOfDate,
    IReadOnlyList<AccountingTrialBalanceRow> Rows,
    long TotalDebitSatang,
    long TotalCreditSatang);

public sealed record AccountingJournalLineReport(
    int LineNo,
    string AccountCode,
    string AccountName,
    long DebitSatang,
    long CreditSatang,
    Guid? MemberId,
    Guid? DeathCaseId,
    int? BeneficiarySlotNo,
    string? PartySnapshot,
    string? DescriptionSnapshot);

public sealed record AccountingJournalEntryReport(
    Guid JournalId,
    string JournalNumber,
    string VoucherType,
    string Description,
    DateOnly BusinessDate,
    bool IsReversal,
    IReadOnlyList<AccountingJournalLineReport> Lines);

public sealed record AccountingJournalReport(
    AccountingBookCode BookCode,
    string BookName,
    AccountingReportPeriod Period,
    IReadOnlyList<AccountingJournalEntryReport> Entries,
    long TotalDebitSatang,
    long TotalCreditSatang);

public sealed record AccountingGeneralLedgerEntry(
    DateOnly BusinessDate,
    string JournalNumber,
    string VoucherType,
    string Description,
    int LineNo,
    long DebitSatang,
    long CreditSatang,
    long RunningDebitBalanceSatang,
    long RunningCreditBalanceSatang);

public sealed record AccountingGeneralLedgerReport(
    AccountingBookCode BookCode,
    string BookName,
    string AccountCode,
    string AccountName,
    AccountingReportPeriod Period,
    long OpeningDebitBalanceSatang,
    long OpeningCreditBalanceSatang,
    long MovementDebitSatang,
    long MovementCreditSatang,
    long ClosingDebitBalanceSatang,
    long ClosingCreditBalanceSatang,
    IReadOnlyList<AccountingGeneralLedgerEntry> Entries);

public sealed record AccountingIncomeExpenseRow(
    string AccountCode,
    string AccountName,
    AccountingAccountType AccountType,
    long DebitSatang,
    long CreditSatang,
    long NetSatang);

public sealed record AccountingIncomeExpenseReport(
    AccountingBookCode BookCode,
    string BookName,
    AccountingReportPeriod Period,
    IReadOnlyList<AccountingIncomeExpenseRow> Rows,
    long TotalIncomeSatang,
    long TotalExpenseSatang,
    long NetResultSatang);

public sealed record AccountingFinancialPositionLine(
    string AccountCode,
    string AccountName,
    AccountingAccountType AccountType,
    long AmountSatang);

public sealed record AccountingFinancialPositionReport(
    AccountingBookCode BookCode,
    string BookName,
    DateOnly AsOfDate,
    IReadOnlyList<AccountingFinancialPositionLine> Assets,
    IReadOnlyList<AccountingFinancialPositionLine> Liabilities,
    IReadOnlyList<AccountingFinancialPositionLine> Equity,
    long CurrentResultSatang,
    long TotalAssetsSatang,
    long TotalLiabilitiesAndEquitySatang,
    bool IsBalanced);

public sealed record AccountingMemberBalanceRow(
    Guid MemberId,
    string MemberRunNo,
    string MemberName,
    long AdvanceSatang,
    long ShortfallSatang,
    long NetAdvanceSatang);

public sealed record AccountingMemberBalancesReport(
    DateOnly AsOfDate,
    IReadOnlyList<AccountingMemberBalanceRow> Rows,
    long TotalAdvanceSatang,
    long TotalShortfallSatang,
    long NetAdvanceSatang);

public sealed record AccountingBeneficiaryUnpaidRow(
    Guid DeathCaseId,
    int BeneficiarySlotNo,
    string BeneficiaryName,
    long PayableSatang,
    long PaidSatang,
    long UnpaidSatang);

public sealed record AccountingBeneficiaryUnpaidReport(
    DateOnly AsOfDate,
    IReadOnlyList<AccountingBeneficiaryUnpaidRow> Rows,
    long TotalPayableSatang,
    long TotalPaidSatang,
    long TotalUnpaidSatang);
