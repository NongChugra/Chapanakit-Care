using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed class CollectionRequestMemberInput
{
    public Guid MemberId { get; set; }
    public bool Selected { get; set; }
    public string? Amount { get; set; }
}

public sealed class CollectionReceiptInput
{
    public Guid CollectionId { get; set; }
    public Guid MemberId { get; set; }
    public bool Selected { get; set; }
    public string? Amount { get; set; }
}

public sealed class CollectionsModel : PageModel
{
    private readonly AppDbContext database;

    public CollectionsModel(AppDbContext database)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    [BindProperty(SupportsGet = true)] public string? GroupNo { get; set; }
    [BindProperty(SupportsGet = true)] public string? CycleKeyFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? AsOf { get; set; }
    [BindProperty] public string? Action { get; set; }
    [BindProperty] public string? RequestCycleKey { get; set; }
    [BindProperty] public string? RequestBusinessDate { get; set; }
    [BindProperty] public string? RequestDueDate { get; set; }
    [BindProperty] public string? RequestDescription { get; set; }
    [BindProperty] public string? RequestToken { get; set; }
    [BindProperty] public List<CollectionRequestMemberInput> RequestLines { get; set; } = [];
    [BindProperty] public string? ReceiptDate { get; set; }
    [BindProperty] public string? ReceiptMoneyAccount { get; set; }
    [BindProperty] public string? ReceiptDescription { get; set; }
    [BindProperty] public string? ReceiptToken { get; set; }
    [BindProperty] public List<CollectionReceiptInput> ReceiptLines { get; set; } = [];

    public IReadOnlyList<Member> Members { get; private set; } = [];
    public IReadOnlyList<AccountingAccount> MoneyAccounts { get; private set; } = [];
    public WelfareCollectionBalanceReport? BalanceReport { get; private set; }
    public AccountingDocumentData? Document { get; private set; }
    public string Output { get; private set; } = "sheet";

    public async Task<IActionResult> OnGetAsync(string? format = null, string? output = null)
    {
        SetGetDefaults();
        try
        {
            var asOfDate = ParseAsOfDate();
            await LoadAsync(asOfDate);
            Output = NormalizeOutput(output);
            var document = CreateDocument(BalanceReport!, Output);
            Document = document;
            if (string.IsNullOrWhiteSpace(format))
                return Page();

            var normalizedFormat = format.Trim().ToLowerInvariant();
            if (normalizedFormat is not ("pdf" or "csv"))
                return BadRequest("กรุณาเลือกไฟล์ PDF หรือ CSV");
            var bytes = normalizedFormat == "pdf"
                ? AccountingDocumentRenderer.GeneratePdf(document)
                : AccountingDocumentRenderer.GenerateCsv(document);
            var contentType = normalizedFormat == "pdf" ? "application/pdf" : "text/csv; charset=utf-8";
            return File(bytes, contentType, $"collection-{Output}.{normalizedFormat}");
        }
        catch (Exception error) when (IsExpectedValidationError(error))
        {
            ModelState.AddModelError(string.Empty, DisplayValidationError(error));
            await LoadFormOptionsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var action = NormalizeAction(Action);
        RemoveIrrelevantModelState(action);
        if (!ModelState.IsValid)
            return await ReturnPageAsync();

        try
        {
            var now = DateTimeOffset.UtcNow;
            const string actor = "ผู้ใช้งานเครื่องนี้";
            if (action == "create")
            {
                var lines = RequestLines
                    .Where(line => line.Selected)
                    .Select(line => new WelfareCollectionLine(line.MemberId, FinanceMoney.Parse(line.Amount ?? string.Empty)))
                    .ToList();
                await new WelfareCollectionService(database).CreateAsync(new(
                    RequestCycleKey ?? string.Empty,
                    ThaiBuddhistDate.Parse(RequestBusinessDate ?? string.Empty),
                    ThaiBuddhistDate.Parse(RequestDueDate ?? string.Empty),
                    RequestDescription ?? string.Empty,
                    RequestToken ?? string.Empty,
                    lines), now, actor);
                return RedirectToPage(new { GroupNo, CycleKeyFilter = RequestCycleKey, AsOf });
            }

            if (action == "receipt")
            {
                var lines = ReceiptLines
                    .Where(line => line.Selected)
                    .Select(line => new MemberReceipt(
                        line.MemberId,
                        FinanceMoney.Parse(line.Amount ?? string.Empty),
                        line.CollectionId))
                    .ToList();
                var journalId = await new FinanceOperationService(database).ReceiveAsync(new(
                    ThaiBuddhistDate.Parse(ReceiptDate ?? string.Empty),
                    ReceiptMoneyAccount ?? string.Empty,
                    ReceiptDescription ?? string.Empty,
                    ReceiptToken ?? string.Empty,
                    lines), now, actor);
                return RedirectToPage("./Voucher", new { id = journalId });
            }

            throw new AccountingValidationException("กรุณาเลือกการสร้างใบเรียกเก็บหรือรับเงินจริง");
        }
        catch (Exception error) when (IsExpectedValidationError(error))
        {
            ModelState.AddModelError(string.Empty, DisplayValidationError(error));
            return await ReturnPageAsync();
        }
    }

    private async Task<IActionResult> ReturnPageAsync()
    {
        EnsureAsOfDefault();
        try
        {
            await LoadAsync(ParseAsOfDate());
            Output = "sheet";
            Document = CreateDocument(BalanceReport!, Output);
        }
        catch (Exception error) when (IsExpectedValidationError(error))
        {
            if (!ModelState.Values.SelectMany(value => value.Errors)
                .Any(value => string.Equals(value.ErrorMessage, DisplayValidationError(error), StringComparison.Ordinal)))
                ModelState.AddModelError(string.Empty, DisplayValidationError(error));
            await LoadFormOptionsAsync();
        }
        return Page();
    }

    private async Task LoadAsync(DateOnly asOfDate)
    {
        await LoadFormOptionsAsync();
        BalanceReport = await new CollectionReportService(database).GetBalancesAsync(
            asOfDate,
            NormalizeFilter(CycleKeyFilter),
            NormalizeFilter(GroupNo));
        if (RequestLines.Count == 0)
            RequestLines = Members.Select(member => new CollectionRequestMemberInput { MemberId = member.Id }).ToList();
        if (ReceiptLines.Count == 0)
            ReceiptLines = BalanceReport.Rows
                .Where(row => row.OutstandingSatang > 0)
                .Select(row => new CollectionReceiptInput { CollectionId = row.CollectionId, MemberId = row.MemberId })
                .ToList();
    }

    private async Task LoadFormOptionsAsync()
    {
        var normalizedGroupNo = NormalizeFilter(GroupNo);
        var members = database.Members.AsNoTracking()
            .Where(member => member.Status == MemberStatus.Normal && member.ArchivedAtUtc == null);
        if (normalizedGroupNo is not null)
            members = members.Where(member => member.GroupNo == normalizedGroupNo);
        Members = await members.OrderBy(member => member.RunNo).ToListAsync();
        MoneyAccounts = await (
                from account in database.AccountingAccounts.AsNoTracking()
                join book in database.AccountingBooks.AsNoTracking() on account.BookId equals book.Id
                where book.Code == AccountingBookCode.Welfare &&
                      (account.Role == AccountingAccountRole.Cash || account.Role == AccountingAccountRole.Bank)
                orderby account.Code
                select account)
            .ToListAsync();
    }

    private void SetGetDefaults()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (string.IsNullOrWhiteSpace(AsOf)) AsOf = ThaiBuddhistDate.Format(today);
        if (string.IsNullOrWhiteSpace(RequestCycleKey)) RequestCycleKey = $"{today.Year + 543:0000}-{today.Month:00}";
        if (string.IsNullOrWhiteSpace(RequestBusinessDate)) RequestBusinessDate = ThaiBuddhistDate.Format(today);
        if (string.IsNullOrWhiteSpace(RequestDueDate)) RequestDueDate = ThaiBuddhistDate.Format(today);
        if (string.IsNullOrWhiteSpace(RequestDescription)) RequestDescription = "เรียกเก็บเงินสงเคราะห์";
        if (string.IsNullOrWhiteSpace(ReceiptDate)) ReceiptDate = ThaiBuddhistDate.Format(today);
        if (string.IsNullOrWhiteSpace(ReceiptDescription)) ReceiptDescription = "รับเงินจริงตามใบเรียกเก็บ";
        if (string.IsNullOrWhiteSpace(RequestToken)) RequestToken = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(ReceiptToken)) ReceiptToken = Guid.NewGuid().ToString("N");
    }

    private void EnsureAsOfDefault()
    {
        if (!string.IsNullOrWhiteSpace(AsOf)) return;
        AsOf = ThaiBuddhistDate.Format(DateOnly.FromDateTime(DateTime.Today));
        ModelState.Remove(nameof(AsOf));
    }

    private DateOnly ParseAsOfDate() => ThaiBuddhistDate.Parse(AsOf ?? string.Empty);

    private static string NormalizeAction(string? action) => action?.Trim().ToLowerInvariant() switch
    {
        "create" => "create",
        "receipt" => "receipt",
        _ => string.Empty
    };

    private static string NormalizeOutput(string? output) => output?.Trim().ToLowerInvariant() switch
    {
        null or "" or "sheet" => "sheet",
        "outstanding" => "outstanding",
        "reminders" => "reminders",
        _ => throw new AccountingValidationException("กรุณาเลือกเอกสารรอบเรียกเก็บที่รองรับ")
    };

    private static AccountingDocumentData CreateDocument(WelfareCollectionBalanceReport report, string output) => output switch
    {
        "sheet" => CollectionReportDocuments.ToCollectionSheet(report),
        "outstanding" => CollectionReportDocuments.ToOutstandingDocument(report),
        "reminders" => CollectionReportDocuments.ToReminderDocument(report),
        _ => throw new AccountingValidationException("กรุณาเลือกเอกสารรอบเรียกเก็บที่รองรับ")
    };

    private void RemoveIrrelevantModelState(string action)
    {
        if (action == "create")
        {
            ModelState.Remove(nameof(ReceiptDate));
            ModelState.Remove(nameof(ReceiptMoneyAccount));
            ModelState.Remove(nameof(ReceiptDescription));
            ModelState.Remove(nameof(ReceiptToken));
            ModelState.Remove(nameof(ReceiptLines));
            return;
        }
        if (action == "receipt")
        {
            ModelState.Remove(nameof(RequestCycleKey));
            ModelState.Remove(nameof(RequestBusinessDate));
            ModelState.Remove(nameof(RequestDueDate));
            ModelState.Remove(nameof(RequestDescription));
            ModelState.Remove(nameof(RequestToken));
            ModelState.Remove(nameof(RequestLines));
        }
    }

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsExpectedValidationError(Exception error) => error is FormatException
        or ArgumentException
        or OverflowException
        or MemberValidationException
        or AccountingValidationException
        or AccountingIdempotencyConflictException
        or AccountingPeriodClosedException;

    private static string DisplayValidationError(Exception error) => error is FormatException or ArgumentException or OverflowException
        ? "กรุณาตรวจสอบวันที่ พ.ศ. และจำนวนเงินบาทที่มีทศนิยมไม่เกิน 2 ตำแหน่ง"
        : error.Message;
}
