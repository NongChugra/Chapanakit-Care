using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed record AccountingReportChoice(string Value, string Label, bool UsesPeriod, bool UsesAccount);

public sealed class ReportsModel : PageModel
{
    private readonly AppDbContext database;

    public ReportsModel(AppDbContext database)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public static IReadOnlyList<AccountingReportChoice> ReportChoices { get; } =
    [
        new("trialbalance", "งบทดลอง", false, false),
        new("journal", "สมุดรายวันทั่วไป", true, false),
        new("generalLedger", "บัญชีแยกประเภท", true, true),
        new("incomeexpense", "รายงานรายได้และค่าใช้จ่าย", true, false),
        new("financialposition", "งบแสดงฐานะการเงิน", false, false),
        new("memberbalances", "ยอดเงินสงเคราะห์ล่วงหน้ารายสมาชิก", false, false),
        new("beneficiaryunpaid", "เงินสงเคราะห์ค้างจ่ายผู้รับผลประโยชน์", false, false)
    ];

    [BindProperty(SupportsGet = true)] public string Report { get; set; } = "trialbalance";
    [BindProperty(SupportsGet = true)] public string Book { get; set; } = "welfare";
    [BindProperty(SupportsGet = true)] public string From { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string To { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string AsOf { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string AccountCode { get; set; } = "";

    public IReadOnlyList<AccountingBook> Books { get; private set; } = [];
    public IReadOnlyList<AccountingAccount> Accounts { get; private set; } = [];
    public AccountingDocumentData? Document { get; private set; }
    public AccountingTrialBalanceReport? TrialBalance { get; private set; }
    public AccountingJournalReport? Journal { get; private set; }
    public AccountingGeneralLedgerReport? GeneralLedger { get; private set; }
    public AccountingIncomeExpenseReport? IncomeExpense { get; private set; }
    public AccountingFinancialPositionReport? FinancialPosition { get; private set; }
    public AccountingMemberBalancesReport? MemberBalances { get; private set; }
    public AccountingBeneficiaryUnpaidReport? BeneficiaryUnpaid { get; private set; }

    public bool UsesPeriod => SelectedChoice?.UsesPeriod == true;
    public bool UsesAsOf => SelectedChoice is { UsesPeriod: false };
    public bool UsesAccount => SelectedChoice?.UsesAccount == true;
    public string ReportTitle => Document?.Title ?? SelectedChoice?.Label ?? "รายงานบัญชี";
    private AccountingReportChoice? SelectedChoice { get; set; }

    public async Task<IActionResult> OnGetAsync(string? format = null)
    {
        ClearReport();
        await LoadBooksAsync();

        var selectedChoice = FindChoice(Report);
        if (selectedChoice is null)
        {
            ModelState.AddModelError(nameof(Report), "กรุณาเลือกประเภทรายงานที่รองรับ");
            return Page();
        }

        SelectedChoice = selectedChoice;
        if (!selectedChoice.UsesAccount) ModelState.Remove(nameof(AccountCode));
        if (selectedChoice.UsesPeriod) ModelState.Remove(nameof(AsOf));
        else
        {
            ModelState.Remove(nameof(From));
            ModelState.Remove(nameof(To));
        }
        Report = selectedChoice.Value;
        ModelState.Remove(nameof(Report));
        if (!TryParseBook(Book, out var bookCode))
        {
            ModelState.AddModelError(nameof(Book), "กรุณาเลือกสมุดบัญชีที่รองรับ");
            return Page();
        }

        Book = ToBookKey(bookCode);
        ModelState.Remove(nameof(Book));
        await LoadAccountsAsync(bookCode);

        try
        {
            object report = selectedChoice.Value switch
            {
                "trialbalance" => await LoadTrialBalanceAsync(bookCode),
                "journal" => await LoadJournalAsync(bookCode),
                "generalLedger" => await LoadGeneralLedgerAsync(bookCode),
                "incomeexpense" => await LoadIncomeExpenseAsync(bookCode),
                "financialposition" => await LoadFinancialPositionAsync(bookCode),
                "memberbalances" => await LoadMemberBalancesAsync(bookCode),
                "beneficiaryunpaid" => await LoadBeneficiaryUnpaidAsync(bookCode),
                _ => throw new AccountingValidationException("กรุณาเลือกประเภทรายงานที่รองรับ")
            };

            SetReport(report);
        }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException
            or AccountingValidationException)
        {
            ModelState.AddModelError(string.Empty, error is FormatException or ArgumentException or OverflowException
                ? "กรุณาตรวจสอบวันที่ พ.ศ. และรหัสบัญชีของสมุดบัญชีที่เลือก" : error.Message);
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(format))
        {
            var normalizedFormat = format.Trim().ToLowerInvariant();
            if (normalizedFormat is not ("pdf" or "csv"))
                return BadRequest("กรุณาเลือกไฟล์ PDF หรือ CSV");

            var bytes = normalizedFormat == "pdf"
                ? GeneratePdf()
                : GenerateCsv();
            var extension = normalizedFormat;
            var contentType = normalizedFormat == "pdf" ? "application/pdf" : "text/csv; charset=utf-8";
            return File(bytes, contentType, $"accounting-{Report}-{Book}.{extension}");
        }

        return Page();
    }

    private async Task LoadBooksAsync() =>
        Books = await database.AccountingBooks.AsNoTracking().OrderBy(book => book.Code).ToListAsync();

    private async Task LoadAccountsAsync(AccountingBookCode bookCode)
    {
        if (!UsesAccount)
        {
            Accounts = [];
            return;
        }

        Accounts = await (from account in database.AccountingAccounts.AsNoTracking()
                          join book in database.AccountingBooks.AsNoTracking() on account.BookId equals book.Id
                          where book.Code == bookCode
                          orderby account.Code
                          select account).ToListAsync();
    }

    private async Task<object> LoadTrialBalanceAsync(AccountingBookCode bookCode)
    {
        if (!TryParseAsOf(out var asOfDate))
            throw new AccountingValidationException("กรุณาระบุวันที่อ้างอิงเป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");

        return await new AccountingReportQueryService(database).GetTrialBalanceAsync(bookCode, asOfDate);
    }

    private async Task<object> LoadJournalAsync(AccountingBookCode bookCode)
    {
        if (!TryParsePeriod(out var period))
            throw new AccountingValidationException("กรุณาระบุช่วงวันที่เป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");

        return await new AccountingReportQueryService(database).GetJournalAsync(bookCode, period);
    }

    private async Task<object> LoadGeneralLedgerAsync(AccountingBookCode bookCode)
    {
        if (!TryParsePeriod(out var period))
            throw new AccountingValidationException("กรุณาระบุช่วงวันที่เป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");
        if (string.IsNullOrWhiteSpace(AccountCode))
            throw new AccountingValidationException("กรุณาเลือกรหัสบัญชีจากสมุดบัญชีที่เลือก");

        return await new AccountingReportQueryService(database).GetGeneralLedgerAsync(bookCode, AccountCode, period);
    }

    private async Task<object> LoadIncomeExpenseAsync(AccountingBookCode bookCode)
    {
        if (!TryParsePeriod(out var period))
            throw new AccountingValidationException("กรุณาระบุช่วงวันที่เป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");

        return await new AccountingReportQueryService(database).GetIncomeExpenseAsync(bookCode, period);
    }

    private async Task<object> LoadFinancialPositionAsync(AccountingBookCode bookCode)
    {
        if (!TryParseAsOf(out var asOfDate))
            throw new AccountingValidationException("กรุณาระบุวันที่อ้างอิงเป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");

        return await new AccountingReportQueryService(database).GetFinancialPositionAsync(bookCode, asOfDate);
    }

    private async Task<object> LoadMemberBalancesAsync(AccountingBookCode bookCode)
    {
        RequireWelfareBook(bookCode);
        if (!TryParseAsOf(out var asOfDate))
            throw new AccountingValidationException("กรุณาระบุวันที่อ้างอิงเป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");

        return await new AccountingReportQueryService(database).GetMemberBalancesAsync(asOfDate);
    }

    private async Task<object> LoadBeneficiaryUnpaidAsync(AccountingBookCode bookCode)
    {
        RequireWelfareBook(bookCode);
        if (!TryParseAsOf(out var asOfDate))
            throw new AccountingValidationException("กรุณาระบุวันที่อ้างอิงเป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");

        return await new AccountingReportQueryService(database).GetBeneficiaryUnpaidAsync(asOfDate);
    }

    private bool TryParseAsOf(out DateOnly asOfDate)
    {
        EnsureDateDefaults();
        return TryParseDate(AsOf, nameof(AsOf), out asOfDate);
    }

    private bool TryParsePeriod(out AccountingReportPeriod period)
    {
        EnsureDateDefaults();
        var fromValid = TryParseDate(From, nameof(From), out var from);
        var toValid = TryParseDate(To, nameof(To), out var to);
        if (!fromValid || !toValid)
        {
            period = default!;
            return false;
        }

        if (from > to)
        {
            ModelState.AddModelError(nameof(From), "วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด");
            period = default!;
            return false;
        }

        period = new AccountingReportPeriod(from, to);
        return true;
    }

    private bool TryParseDate(string value, string fieldName, out DateOnly date)
    {
        try
        {
            date = ThaiBuddhistDate.Parse((value ?? string.Empty).Trim());
            return true;
        }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException)
        {
            ModelState.AddModelError(fieldName, "วันที่ต้องเป็น พ.ศ. รูปแบบ วว/ดด/ปปปป");
            date = default;
            return false;
        }
    }

    private void EnsureDateDefaults()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (string.IsNullOrWhiteSpace(AsOf))
        {
            AsOf = ThaiBuddhistDate.Format(today);
            ModelState.Remove(nameof(AsOf));
        }
        if (string.IsNullOrWhiteSpace(From))
        {
            From = ThaiBuddhistDate.Format(new DateOnly(today.Year, today.Month, 1));
            ModelState.Remove(nameof(From));
        }
        if (string.IsNullOrWhiteSpace(To))
        {
            To = ThaiBuddhistDate.Format(today);
            ModelState.Remove(nameof(To));
        }
    }

    private void SetReport(object report)
    {
        switch (report)
        {
            case AccountingTrialBalanceReport value:
                TrialBalance = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            case AccountingJournalReport value:
                Journal = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            case AccountingGeneralLedgerReport value:
                GeneralLedger = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            case AccountingIncomeExpenseReport value:
                IncomeExpense = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            case AccountingFinancialPositionReport value:
                FinancialPosition = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            case AccountingMemberBalancesReport value:
                MemberBalances = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            case AccountingBeneficiaryUnpaidReport value:
                BeneficiaryUnpaid = value;
                Document = AccountingReportDocuments.ToDocumentData(value);
                break;
            default:
                throw new AccountingValidationException("ไม่พบข้อมูลรายงานที่เลือก");
        }
    }

    private byte[] GeneratePdf()
    {
        if (Document is not { } document)
            throw new InvalidOperationException("ไม่พบข้อมูลรายงานสำหรับสร้าง PDF");
        return AccountingDocumentRenderer.GeneratePdf(document);
    }

    private byte[] GenerateCsv()
    {
        if (Document is not { } document)
            throw new InvalidOperationException("ไม่พบข้อมูลรายงานสำหรับสร้าง CSV");
        return AccountingDocumentRenderer.GenerateCsv(document);
    }

    private void ClearReport()
    {
        Document = null;
        TrialBalance = null;
        Journal = null;
        GeneralLedger = null;
        IncomeExpense = null;
        FinancialPosition = null;
        MemberBalances = null;
        BeneficiaryUnpaid = null;
        Accounts = [];
    }

    private static AccountingReportChoice? FindChoice(string? value)
    {
        var normalized = NormalizeKey(value);
        return ReportChoices.FirstOrDefault(choice => NormalizeKey(choice.Value) == normalized);
    }

    private static string NormalizeKey(string? value) =>
        string.Concat((value ?? string.Empty).Trim().Where(character => character is not '-' and not '_' and not ' ')).ToLowerInvariant();

    private static bool TryParseBook(string? value, out AccountingBookCode bookCode)
    {
        switch (NormalizeKey(value))
        {
            case "welfare":
                bookCode = AccountingBookCode.Welfare;
                return true;
            case "association":
                bookCode = AccountingBookCode.Association;
                return true;
            default:
                bookCode = default;
                return false;
        }
    }

    private static string ToBookKey(AccountingBookCode bookCode) => bookCode switch
    {
        AccountingBookCode.Welfare => "welfare",
        AccountingBookCode.Association => "association",
        _ => throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก")
    };

    private void RequireWelfareBook(AccountingBookCode bookCode)
    {
        if (bookCode != AccountingBookCode.Welfare)
            throw new AccountingValidationException("รายงานนี้ใช้เฉพาะสมุดบัญชีเงินสงเคราะห์สมาชิก");
    }
}
