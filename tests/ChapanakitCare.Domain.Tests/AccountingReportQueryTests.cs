using System.Text;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingReportQueryTests
{
    private static readonly DateOnly OpeningDate = new(2026, 9, 1);
    private static readonly DateOnly ReceiptDate = new(2026, 9, 10);
    private static readonly DateOnly ReversedReceiptDate = new(2026, 9, 11);
    private static readonly DateOnly ReversalDate = new(2026, 9, 12);
    private static readonly DateOnly AccrualDate = new(2026, 9, 13);
    private static readonly DateOnly PayoutDate = new(2026, 9, 14);

    [Fact]
    public async Task Trial_balance_uses_business_date_cutoff_keeps_books_separate_and_does_not_mutate_journals()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var service = new AccountingReportQueryService(store.Context);
        var journalsBefore = await store.Context.AccountingJournals.CountAsync();
        var linesBefore = await store.Context.AccountingJournalLines.CountAsync();

        var beforeReversal = await service.GetTrialBalanceAsync(AccountingBookCode.Welfare, ReversedReceiptDate);
        var afterReversal = await service.GetTrialBalanceAsync(AccountingBookCode.Welfare, ReversalDate);

        Assert.Equal(AccountingBookCode.Welfare, beforeReversal.BookCode);
        Assert.Equal(28_351, Assert.Single(beforeReversal.Rows, x => x.AccountCode == "1000").DebitBalanceSatang);
        Assert.Equal(28_351, Assert.Single(beforeReversal.Rows, x => x.AccountCode == "2000").CreditBalanceSatang);
        Assert.DoesNotContain(beforeReversal.Rows, x => x.AccountCode == "4000");
        Assert.Equal(28_351, beforeReversal.TotalDebitSatang);
        Assert.Equal(28_351, beforeReversal.TotalCreditSatang);
        Assert.Equal(27_451, Assert.Single(afterReversal.Rows, x => x.AccountCode == "1000").DebitBalanceSatang);
        Assert.Equal(27_451, Assert.Single(afterReversal.Rows, x => x.AccountCode == "2000").CreditBalanceSatang);
        Assert.Equal(journalsBefore, await store.Context.AccountingJournals.CountAsync());
        Assert.Equal(linesBefore, await store.Context.AccountingJournalLines.CountAsync());
    }

    [Fact]
    public async Task General_ledger_carries_opening_then_signed_movement_from_a_reversal()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var service = new AccountingReportQueryService(store.Context);

        var ledger = await service.GetGeneralLedgerAsync(AccountingBookCode.Welfare, "1000",
            new AccountingReportPeriod(ReceiptDate, ReversalDate));

        Assert.Equal(27_000, ledger.OpeningDebitBalanceSatang);
        Assert.Equal(0, ledger.OpeningCreditBalanceSatang);
        Assert.Equal(1_351, ledger.MovementDebitSatang);
        Assert.Equal(900, ledger.MovementCreditSatang);
        Assert.Equal(27_451, ledger.ClosingDebitBalanceSatang);
        Assert.Equal(0, ledger.ClosingCreditBalanceSatang);
        Assert.Equal(3, ledger.Entries.Count);
        Assert.Equal(ReversalDate, ledger.Entries[2].BusinessDate);
        Assert.Equal(900, ledger.Entries[2].CreditSatang);
        Assert.Equal(27_451, ledger.Entries[2].RunningDebitBalanceSatang);
    }

    [Fact]
    public async Task Journal_report_marks_the_posted_reversal_without_omitting_its_signed_lines()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var service = new AccountingReportQueryService(store.Context);

        var journal = await service.GetJournalAsync(AccountingBookCode.Welfare,
            new AccountingReportPeriod(ReceiptDate, ReversalDate));

        Assert.Equal(3, journal.Entries.Count);
        var reversal = Assert.Single(journal.Entries, x => x.BusinessDate == ReversalDate);
        Assert.True(reversal.IsReversal);
        Assert.Equal(900, reversal.Lines.Sum(x => x.DebitSatang));
        Assert.Equal(900, reversal.Lines.Sum(x => x.CreditSatang));
        Assert.Equal(2_251, journal.TotalDebitSatang);
        Assert.Equal(2_251, journal.TotalCreditSatang);
    }

    [Fact]
    public async Task Income_expense_report_shows_only_the_association_fee_income_and_operating_expense()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var service = new AccountingReportQueryService(store.Context);
        var period = new AccountingReportPeriod(ReceiptDate, ReversedReceiptDate);

        var association = await service.GetIncomeExpenseAsync(AccountingBookCode.Association, period);
        var welfare = await service.GetIncomeExpenseAsync(AccountingBookCode.Welfare, period);

        var fee = Assert.Single(association.Rows, x => x.AccountCode == "4000");
        Assert.Equal(AccountingAccountType.Income, fee.AccountType);
        Assert.Equal(100, fee.NetSatang);
        var expense = Assert.Single(association.Rows, x => x.AccountCode == "5000");
        Assert.Equal(AccountingAccountType.Expense, expense.AccountType);
        Assert.Equal(60, expense.NetSatang);
        Assert.Equal(100, association.TotalIncomeSatang);
        Assert.Equal(60, association.TotalExpenseSatang);
        Assert.Equal(40, association.NetResultSatang);
        Assert.Empty(welfare.Rows);
        Assert.Equal(0, welfare.NetResultSatang);
    }

    [Fact]
    public async Task Financial_position_keeps_the_association_fee_result_with_its_own_book()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var service = new AccountingReportQueryService(store.Context);

        var position = await service.GetFinancialPositionAsync(AccountingBookCode.Association, ReversedReceiptDate);

        Assert.Equal(40, Assert.Single(position.Assets, x => x.AccountCode == "1000").AmountSatang);
        Assert.Equal(100, Assert.Single(position.Assets, x => x.AccountCode == "1300").AmountSatang);
        Assert.Equal(100, Assert.Single(position.Equity, x => x.AccountCode == "3000").AmountSatang);
        Assert.Equal(40, position.CurrentResultSatang);
        Assert.Equal(140, position.TotalAssetsSatang);
        Assert.Equal(140, position.TotalLiabilitiesAndEquitySatang);
        Assert.True(position.IsBalanced);
    }

    [Fact]
    public async Task Member_and_beneficiary_reports_derive_control_balances_from_dimensions()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        var fixture = await SeedAsync(store.Context);
        var service = new AccountingReportQueryService(store.Context);

        var members = await service.GetMemberBalancesAsync(ReversalDate);
        var benefits = await service.GetBeneficiaryUnpaidAsync(PayoutDate);

        var member = Assert.Single(members.Rows, x => x.MemberId == fixture.MemberId);
        Assert.Equal("00001", member.MemberRunNo);
        Assert.Equal(27_451, member.AdvanceSatang);
        Assert.Equal(0, member.ShortfallSatang);
        Assert.Equal(27_451, member.NetAdvanceSatang);
        Assert.Equal(27_451, members.TotalAdvanceSatang);
        var beneficiary = Assert.Single(benefits.Rows, x => x.DeathCaseId == fixture.DeathCaseId && x.BeneficiarySlotNo == 1);
        Assert.Equal(3_000, beneficiary.PayableSatang);
        Assert.Equal(1_200, beneficiary.PaidSatang);
        Assert.Equal(1_800, beneficiary.UnpaidSatang);
        Assert.Equal(1_800, benefits.TotalUnpaidSatang);
    }

    [Fact]
    public async Task Control_balance_reports_keep_frozen_party_snapshots_when_the_source_record_is_unavailable()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var posting = new AccountingPostingService(store.Context);
        var formerMemberId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var formerDeathCaseId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        await PostAsync(posting, AccountingBookCode.Welfare, new DateOnly(2026, 9, 15), "w-historical-control", "opening_correction",
            "ยอดควบคุมประวัติ", [
                new("1200", 449, 0, new(MemberId: formerMemberId, PartySnapshot: "99999 สมาชิกเดิม")),
                new("2000", 700, 0),
                new("2200", 0, 449),
                new("2100", 0, 700, new(DeathCaseId: formerDeathCaseId, BeneficiarySlotNo: 1, PartySnapshot: "ผู้รับผลประโยชน์เดิม"))
            ]);
        var service = new AccountingReportQueryService(store.Context);

        var members = await service.GetMemberBalancesAsync(new DateOnly(2026, 9, 15));
        var benefits = await service.GetBeneficiaryUnpaidAsync(new DateOnly(2026, 9, 15));

        var formerMember = Assert.Single(members.Rows, row => row.MemberId == formerMemberId);
        Assert.Equal("99999", formerMember.MemberRunNo);
        Assert.Equal("สมาชิกเดิม", formerMember.MemberName);
        Assert.Equal(0, formerMember.AdvanceSatang);
        Assert.Equal(449, formerMember.ShortfallSatang);
        Assert.Equal(-449, formerMember.NetAdvanceSatang);
        var formerBeneficiary = Assert.Single(benefits.Rows, row => row.DeathCaseId == formerDeathCaseId);
        Assert.Equal("ผู้รับผลประโยชน์เดิม", formerBeneficiary.BeneficiaryName);
        Assert.Equal(700, formerBeneficiary.PayableSatang);
        Assert.Equal(700, formerBeneficiary.UnpaidSatang);
    }

    [Fact]
    public void Trial_balance_document_data_keeps_the_book_cutoff_and_integer_satang_as_numeric_baht()
    {
        var asOfDate = new DateOnly(2026, 9, 30);
        var report = new AccountingTrialBalanceReport(
            AccountingBookCode.Association,
            "บัญชีสมาคม",
            asOfDate,
            [new("1000", "เงินสด", AccountingAccountType.Asset, AccountingNormalBalance.Debit, 12_345, 0)],
            12_345,
            12_345);

        var data = ToDocumentData(report);

        Assert.Equal("งบทดลอง", data.Title);
        Assert.Equal("บัญชีสมาคม", data.BookName);
        Assert.Equal(asOfDate, data.AsOfDate);
        Assert.Null(data.PeriodFrom);
        Assert.Equal(["รหัสบัญชี", "ชื่อบัญชี", "เดบิต (บาท)", "เครดิต (บาท)"], data.Columns.Select(column => column.Heading));
        Assert.Equal(["1000", "เงินสด", "123.45", "0.00"], Assert.Single(data.Rows));
        Assert.Equal(AccountingDocumentColumnType.Numeric, data.Columns[2].Type);
        Assert.Contains("รวมเดบิต 123.45 บาท", data.Notes!);
        Assert.Contains("รวมเครดิต 123.45 บาท", data.Notes!);
    }

    [Fact]
    public void Journal_document_data_and_csv_keep_posted_reversal_lines_visible()
    {
        var period = new AccountingReportPeriod(new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 12));
        var report = new AccountingJournalReport(
            AccountingBookCode.Welfare,
            "บัญชีสวัสดิการสมาชิก",
            period,
            [new(
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                "W-00000004",
                "reversal",
                "กลับรายการรับเงิน",
                period.From,
                true,
                [
                    new(1, "2000", "เงินสงเคราะห์ล่วงหน้าสมาชิก", 900, 0, null, null, null, null, null),
                    new(2, "1000", "เงินสด", 0, 900, null, null, null, null, null)
                ])],
            900,
            900);

        var data = ToDocumentData(report);
        var csv = Encoding.UTF8.GetString(GenerateCsv(report));

        Assert.Equal(period.From, data.PeriodFrom);
        Assert.Equal(period.To, data.PeriodTo);
        Assert.Equal(2, data.Rows.Count);
        Assert.Contains(data.Rows, row => row.SequenceEqual([
            "12/09/2569", "W-00000004", "reversal", "กลับรายการรับเงิน", "1000", "เงินสด", "0.00", "9.00", "กลับรายการ"]));
        Assert.Contains("\"12/09/2569\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"กลับรายการ\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"9.00\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void General_ledger_document_data_carries_opening_movement_and_running_balances()
    {
        var period = new AccountingReportPeriod(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12));
        var report = new AccountingGeneralLedgerReport(
            AccountingBookCode.Welfare,
            "บัญชีสวัสดิการสมาชิก",
            "1000",
            "เงินสด",
            period,
            27_000,
            0,
            1_351,
            900,
            27_451,
            0,
            [new(period.To, "W-00000004", "reversal", "กลับรายการรับเงิน", 2, 0, 900, 27_451, 0)]);

        var data = ToDocumentData(report);

        Assert.Equal("บัญชีแยกประเภท: 1000 เงินสด", data.Title);
        Assert.Equal(["วันที่", "เลขที่ใบสำคัญ", "ประเภท", "รายละเอียด", "เดบิต (บาท)", "เครดิต (บาท)", "ยอดเดบิตคงเหลือ (บาท)", "ยอดเครดิตคงเหลือ (บาท)"],
            data.Columns.Select(column => column.Heading));
        Assert.Equal(["12/09/2569", "W-00000004", "reversal", "กลับรายการรับเงิน", "0.00", "9.00", "274.51", "0.00"], Assert.Single(data.Rows));
        Assert.Contains("ยอดยกมาเดบิต 270.00 บาท", data.Notes!);
        Assert.Contains("ยอดคงเหลือเดบิต 274.51 บาท", data.Notes!);
    }

    [Fact]
    public void Financial_statement_document_data_keeps_association_result_separate_from_assets()
    {
        var period = new AccountingReportPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        var incomeExpense = new AccountingIncomeExpenseReport(
            AccountingBookCode.Association,
            "บัญชีสมาคม",
            period,
            [
                new("4000", "รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4", AccountingAccountType.Income, 0, 100, 100),
                new("5000", "ค่าใช้จ่ายดำเนินงาน", AccountingAccountType.Expense, 60, 0, 60)
            ],
            100,
            60,
            40);
        var position = new AccountingFinancialPositionReport(
            AccountingBookCode.Association,
            "บัญชีสมาคม",
            period.To,
            [new("1000", "เงินสด", AccountingAccountType.Asset, 40)],
            [],
            [new("3000", "ทุนยกมาตามหลักฐาน", AccountingAccountType.Equity, 100)],
            40,
            40,
            140,
            false);

        var incomeExpenseData = ToDocumentData(incomeExpense);
        var positionData = ToDocumentData(position);

        Assert.Equal("รายงานรายได้และค่าใช้จ่าย", incomeExpenseData.Title);
        Assert.Contains(incomeExpenseData.Rows, row => row.SequenceEqual([
            "4000", "รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4", "รายได้", "0.00", "1.00", "1.00"]));
        Assert.Contains("ผลการดำเนินงานสุทธิ 0.40 บาท", incomeExpenseData.Notes!);
        Assert.Equal("งบแสดงฐานะการเงิน", positionData.Title);
        Assert.Equal(period.To, positionData.AsOfDate);
        Assert.Contains(positionData.Rows, row => row.SequenceEqual(["สินทรัพย์", "1000", "เงินสด", "0.40"]));
        Assert.Contains(positionData.Rows, row => row.SequenceEqual(["ส่วนทุน", "", "ผลการดำเนินงานงวดปัจจุบัน", "0.40"]));
        Assert.Contains("งบไม่สมดุล", positionData.Notes!);
    }

    [Fact]
    public void Welfare_control_document_data_keeps_member_and_beneficiary_balances_in_the_welfare_book()
    {
        var asOfDate = new DateOnly(2026, 9, 14);
        var memberId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var deathCaseId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var members = new AccountingMemberBalancesReport(
            asOfDate,
            [new(memberId, "00001", "ทดสอบ สมาชิก", 27_451, 449, 27_002)],
            27_451,
            449,
            27_002);
        var benefits = new AccountingBeneficiaryUnpaidReport(
            asOfDate,
            [new(deathCaseId, 1, "ผู้รับผลประโยชน์ หนึ่ง", 3_000, 1_200, 1_800)],
            3_000,
            1_200,
            1_800);

        var memberData = ToDocumentData(members);
        var benefitData = ToDocumentData(benefits);

        Assert.Equal("บัญชีสวัสดิการสมาชิก", memberData.BookName);
        Assert.Equal(asOfDate, memberData.AsOfDate);
        Assert.Contains(memberData.Rows, row => row.SequenceEqual([
            "00001", "ทดสอบ สมาชิก", "274.51", "4.49", "270.02"]));
        Assert.Contains("ยอดเงินสงเคราะห์ล่วงหน้ารวม 274.51 บาท", memberData.Notes!);
        Assert.Equal("บัญชีสวัสดิการสมาชิก", benefitData.BookName);
        Assert.Contains(benefitData.Rows, row => row.SequenceEqual([
            deathCaseId.ToString(), "1", "ผู้รับผลประโยชน์ หนึ่ง", "30.00", "12.00", "18.00"]));
        Assert.Contains("เงินสงเคราะห์ค้างจ่ายรวม 18.00 บาท", benefitData.Notes!);
    }

    private static AccountingDocumentData ToDocumentData<TReport>(TReport report)
    {
        var type = typeof(AccountingReportQueryService).Assembly
            .GetType("ChapanakitCare.Infrastructure.AccountingReports.AccountingReportDocuments");
        Assert.NotNull(type);
        var method = type!.GetMethod("ToDocumentData", [typeof(TReport)]);
        Assert.NotNull(method);
        return Assert.IsType<AccountingDocumentData>(method!.Invoke(null, [report]));
    }

    private static byte[] GenerateCsv<TReport>(TReport report)
    {
        var type = typeof(AccountingReportQueryService).Assembly
            .GetType("ChapanakitCare.Infrastructure.AccountingReports.AccountingReportDocuments");
        Assert.NotNull(type);
        var method = type!.GetMethod("GenerateCsv", [typeof(TReport)]);
        Assert.NotNull(method);
        return Assert.IsType<byte[]>(method!.Invoke(null, [report]));
    }

    private static async Task<ReportFixture> SeedAsync(AppDbContext database)
    {
        var member = TestData.Member();
        database.Members.Add(member);
        await database.SaveChangesAsync();

        var setup = new AccountingSetupService(database);
        await setup.EnsureCatalogAsync();
        await setup.ActivateBookAsync(new(AccountingBookCode.Welfare, OpeningDate, "ยอดยกมาตามหลักฐาน", Actor()));
        await setup.ActivateBookAsync(new(AccountingBookCode.Association, OpeningDate, "ยอดยกมาตามหลักฐาน", Actor()));
        var posting = new AccountingPostingService(database);
        var memberDimensions = new AccountingDimensions(MemberId: member.Id, PartySnapshot: "00001 ทดสอบ สมาชิก");
        var deathCaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var beneficiaryDimensions = new AccountingDimensions(DeathCaseId: deathCaseId, BeneficiarySlotNo: 1,
            PartySnapshot: "ผู้รับผลประโยชน์ หนึ่ง");

        await PostAsync(posting, AccountingBookCode.Welfare, OpeningDate, "w-open", "opening", "ยอดยกมา",
            [new("1000", 27_000, 0, memberDimensions), new("2000", 0, 27_000, memberDimensions)]);
        await PostAsync(posting, AccountingBookCode.Welfare, ReceiptDate, "w-receipt-one", "advance_receipt", "รับเงิน",
            [new("1000", 451, 0, memberDimensions), new("2000", 0, 451, memberDimensions)]);
        var reversedReceipt = await PostAsync(posting, AccountingBookCode.Welfare, ReversedReceiptDate,
            "w-receipt-two", "advance_receipt", "รับเงินที่กลับรายการ",
            [new("1000", 900, 0, memberDimensions), new("2000", 0, 900, memberDimensions)]);
        await PostAsync(posting, AccountingBookCode.Welfare, ReversalDate, "w-reversal", "reversal", "กลับรายการรับเงิน",
            [new("2000", 900, 0, memberDimensions), new("1000", 0, 900, memberDimensions)], reversedReceipt.JournalId);
        await PostAsync(posting, AccountingBookCode.Welfare, AccrualDate, "w-accrual", "benefit_accrual", "ตั้งหนี้เงินสงเคราะห์",
            [new("2000", 3_000, 0, memberDimensions), new("2100", 0, 3_000, beneficiaryDimensions)]);
        await PostAsync(posting, AccountingBookCode.Welfare, PayoutDate, "w-payout", "benefit_payout", "จ่ายเงินสงเคราะห์",
            [new("2100", 1_200, 0, beneficiaryDimensions), new("1000", 0, 1_200, beneficiaryDimensions)]);

        await PostAsync(posting, AccountingBookCode.Association, OpeningDate, "a-open", "opening", "ยอดยกมา",
            [new("1000", 100, 0), new("3000", 0, 100)]);
        await PostAsync(posting, AccountingBookCode.Association, ReceiptDate, "a-fee", "fee_accrual", "ค่าหักร้อยละ 4",
            [new("1300", 100, 0), new("4000", 0, 100)]);
        await PostAsync(posting, AccountingBookCode.Association, ReversedReceiptDate, "a-expense", "expense", "ค่าใช้จ่าย",
            [new("5000", 60, 0), new("1000", 0, 60)]);

        return new(member.Id, deathCaseId);
    }

    private static Task<AccountingPostingResult> PostAsync(
        AccountingPostingService posting,
        AccountingBookCode bookCode,
        DateOnly date,
        string token,
        string voucherType,
        string description,
        IReadOnlyList<AccountingPostLine> lines,
        Guid? reversesJournalId = null) =>
        posting.PostAsync(new(bookCode, voucherType, description, date, token, token + "-fingerprint", Actor(), lines,
            ReversesJournalId: reversesJournalId));

    private static AccountingActor Actor() => new("report-test", "ผู้ทดสอบรายงาน", "TEST-PC", "test");

    private sealed record ReportFixture(Guid MemberId, Guid DeathCaseId);
}
