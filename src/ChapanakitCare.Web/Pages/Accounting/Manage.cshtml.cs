using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed class ManageModel : PageModel
{
    private readonly AppDbContext database;
    public ManageModel(AppDbContext database) { this.database = database; }
    [BindProperty(SupportsGet = true)] public string Book { get; set; } = "welfare";
    [BindProperty] public string Action { get; set; } = "bank";
    [BindProperty] public string AccountCode { get; set; } = "";
    [BindProperty] public string AccountName { get; set; } = "";
    [BindProperty] public string BankDescription { get; set; } = "";
    [BindProperty] public string From { get; set; } = "";
    [BindProperty] public string To { get; set; } = "";
    [BindProperty] public string AsOf { get; set; } = "";
    [BindProperty] public string StatementBalance { get; set; } = "";
    [BindProperty] public string Evidence { get; set; } = "";
    [BindProperty] public string RequestToken { get; set; } = "";
    public MoneyReconciliation? Reconciliation { get; private set; }
    public IReadOnlyList<AccountingAccount> Accounts { get; private set; } = [];
    public IReadOnlyList<AccountingPeriod> Periods { get; private set; } = [];
    public IReadOnlyList<MoneyReconciliation> Reconciliations { get; private set; } = [];

    public async Task OnGetAsync()
    {
        // An omitted GET selector uses the initialized default book.
        if (Book is "welfare" or "association") ModelState.Remove(nameof(Book));
        var today = DateOnly.FromDateTime(DateTime.Today);
        From = ThaiBuddhistDate.Format(new(today.Year, today.Month, 1));
        To = ThaiBuddhistDate.Format(new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));
        AsOf = ThaiBuddhistDate.Format(today);
        RequestToken = Guid.NewGuid().ToString("N");
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Action != "bank") ModelState.Remove(nameof(BankDescription));
        if (Action is not ("bank" or "expense" or "cash")) ModelState.Remove(nameof(AccountName));
        if (Action is "close" or "reopen") ModelState.Remove(nameof(AccountCode));
        if (Action is not ("close" or "reopen"))
        {
            ModelState.Remove(nameof(From));
            ModelState.Remove(nameof(To));
        }
        if (Action != "reconcile")
        {
            ModelState.Remove(nameof(AsOf));
            ModelState.Remove(nameof(StatementBalance));
            ModelState.Remove(nameof(RequestToken));
        }
        if (Action is "bank" or "expense" or "cash") ModelState.Remove(nameof(Evidence));
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        try
        {
            var code = BookCode();
            var actor = new AccountingActor("local-user", "ผู้ใช้งานเครื่องนี้", Environment.MachineName, "accounting");
            var setup = new AccountingSetupService(database);
            await setup.EnsureCatalogAsync();
            switch (Action)
            {
                case "cash":
                    await setup.AddCashAccountAsync(new(code, AccountCode, AccountName, actor));
                    break;
                case "bank":
                    await setup.AddBankAccountAsync(new(code, AccountCode, AccountName, BankDescription, actor));
                    break;
                case "expense":
                    if (code != AccountingBookCode.Association) throw new AccountingValidationException("หมวดค่าใช้จ่ายใช้กับสมุดบัญชีสมาคมเท่านั้น");
                    await setup.AddExpenseCategoryAsync(new(AccountCode, AccountName, actor));
                    break;
                case "close":
                case "reopen":
                    var command = new AccountingPeriodCommand(code, ThaiBuddhistDate.Parse(From), ThaiBuddhistDate.Parse(To), Evidence, actor);
                    if (Action == "close") await setup.ClosePeriodAsync(command);
                    else await setup.ReopenPeriodAsync(command);
                    break;
                case "reconcile":
                    Reconciliation = await new FinanceOperationService(database).ReconcileAsync(new(Book, AccountCode,
                        ThaiBuddhistDate.Parse(AsOf), FinanceMoney.Parse(StatementBalance, allowZero: true), Evidence, RequestToken), DateTimeOffset.UtcNow, actor.DisplayName);
                    await LoadAsync();
                    return Page();
                default: throw new AccountingValidationException("ไม่รองรับการตั้งค่านี้");
            }
            return RedirectToPage(new { Book });
        }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException or AccountingValidationException or AccountingIdempotencyConflictException or AccountingPeriodClosedException)
        {
            ModelState.AddModelError(string.Empty, error is FormatException or ArgumentException or OverflowException
                ? "กรุณาตรวจสอบวันที่ พ.ศ. และจำนวนเงินบาท" : error.Message);
            await LoadAsync();
            return Page();
        }
    }

    private AccountingBookCode BookCode() => Book switch
    {
        "welfare" => AccountingBookCode.Welfare,
        "association" => AccountingBookCode.Association,
        _ => throw new AccountingValidationException("กรุณาเลือกสมุดบัญชี")
    };

    private async Task LoadAsync()
    {
        if (Book is not ("welfare" or "association")) return;
        var code = BookCode();
        var book = await database.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code);
        if (book is null) return;
        Accounts = await database.AccountingAccounts.AsNoTracking().Where(x => x.BookId == book.Id).OrderBy(x => x.Code).ToListAsync();
        Periods = await database.AccountingPeriods.AsNoTracking().Where(x => x.BookId == book.Id).OrderByDescending(x => x.StartsOn).ToListAsync();
        var ids = Accounts.Select(x => x.Id.ToString()).ToArray();
        var saved = await database.AuditEvents.AsNoTracking().Where(x => x.Action == "accounting.reconciled" && ids.Contains(x.EntityId))
            .Select(x => x.Reason).ToListAsync();
        Reconciliations = saved.Select(x => JsonSerializer.Deserialize<MoneyReconciliation>(x ?? "null"))
            .OfType<MoneyReconciliation>().OrderByDescending(x => x.Date).ToList();
    }
}
