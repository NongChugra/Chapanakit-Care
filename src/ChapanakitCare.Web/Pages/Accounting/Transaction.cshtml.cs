using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Domain;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed class TransactionModel : PageModel
{
    private readonly AppDbContext database;
    public TransactionModel(AppDbContext database) { this.database = database; }
    [BindProperty] public string Kind { get; set; } = "receipt";
    [BindProperty] public string BusinessDate { get; set; } = "";
    [BindProperty] public string Amount { get; set; } = "";
    [BindProperty] public string Evidence { get; set; } = "";
    [BindProperty] public string RequestToken { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Book { get; set; } = "welfare";
    [BindProperty] public string MoneyAccount { get; set; } = "1000";
    [BindProperty] public string TargetAccount { get; set; } = "1100";
    [BindProperty] public string ExpenseAccount { get; set; } = "5000";
    [BindProperty] public string Payee { get; set; } = "";
    [BindProperty] public Guid MemberId { get; set; }
    [BindProperty] public Guid? CollectionId { get; set; }
    [BindProperty] public Guid DeathCaseId { get; set; }
    [BindProperty] public Guid OriginalJournalId { get; set; }
    [BindProperty] public int BeneficiarySlot { get; set; } = 1;
    public IReadOnlyList<AccountingAccount> Accounts { get; private set; } = [];
    public IReadOnlyList<Member> Members { get; private set; } = [];
    public IReadOnlyList<AccountingAccount> AssociationAccounts { get; private set; } = [];
    public IReadOnlyList<DeathBeneficiarySnapshot> Recipients { get; private set; } = [];
    public IReadOnlyList<AccountingJournal> ExpenseDocuments { get; private set; } = [];
    public string Heading => Kind switch { "expense" => "จ่ายค่าใช้จ่ายสมาคม", "transfer" => "โอนเงินสด / ธนาคาร", "benefit" => "จ่ายเงินสงเคราะห์ผู้รับผลประโยชน์", "remittance" => "นำส่งค่าหักให้สมาคม", "refund" => "จ่ายคืนเงินล่วงหน้าคงเหลือ", "reclassify" => "แก้หมวดค่าใช้จ่าย", _ => "รับเงินสงเคราะห์สมาชิก" };
    public async Task OnGetAsync(string kind = "receipt")
    {
        if (Book is "welfare" or "association") ModelState.Remove(nameof(Book));
        Kind = kind;
        BusinessDate = ThaiBuddhistDate.Format(DateOnly.FromDateTime(DateTime.Today));
        RequestToken = Guid.NewGuid().ToString("N");
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Kind is not ("transfer" or "remittance" or "reclassify")) ModelState.Remove(nameof(TargetAccount));
        if (Kind == "reclassify") ModelState.Remove(nameof(MoneyAccount));
        if (Kind is not ("expense" or "reclassify")) ModelState.Remove(nameof(ExpenseAccount));
        if (Kind != "expense")
        {
            ModelState.Remove(nameof(Payee));
        }
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        try
        {
            var date = ThaiBuddhistDate.Parse(BusinessDate);
            var amount = FinanceMoney.Parse(Amount);
            var service = new FinanceOperationService(database);
            var now = DateTimeOffset.UtcNow;
            const string actor = "ผู้ใช้งานเครื่องนี้";
            var id = Kind switch
            {
                "receipt" => await service.ReceiveAsync(new(date, MoneyAccount, Evidence, RequestToken, [new(MemberId, amount, CollectionId)]), now, actor),
                "expense" => await service.PayExpenseAsync(new(date, MoneyAccount, ExpenseAccount, amount, Payee, Evidence, RequestToken), now, actor),
                "transfer" => await service.TransferAsync(new(Book, date, MoneyAccount, TargetAccount, amount, Evidence, RequestToken), now, actor),
                "benefit" => await service.PayBenefitAsync(new(DeathCaseId, BeneficiarySlot, date, MoneyAccount, amount, Evidence, RequestToken), now, actor),
                "remittance" => await service.RemitFeeAsync(new(date, MoneyAccount, TargetAccount, amount, Evidence, RequestToken), now, actor),
                "refund" => await service.RefundAsync(new(MemberId, date, MoneyAccount, amount, Evidence, RequestToken), now, actor),
                "reclassify" => await service.ReclassifyExpenseAsync(new(OriginalJournalId, date, ExpenseAccount, TargetAccount, amount, Evidence, RequestToken), now, actor),
                _ => throw new AccountingValidationException("กรุณาเลือกประเภทรายการที่รองรับ")
            };
            return RedirectToPage("./Voucher", new { id });
        }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException
            or AccountingValidationException or AccountingIdempotencyConflictException or AccountingPeriodClosedException)
        {
            ModelState.AddModelError(string.Empty, error is FormatException or ArgumentException or OverflowException
                ? "กรุณาตรวจสอบวันที่ พ.ศ. และจำนวนเงินบาทที่มีทศนิยมไม่เกิน 2 ตำแหน่ง" : error.Message);
            await LoadAsync();
            return Page();
        }
    }

    private async Task LoadAsync()
    {
        if (Kind is "receipt" or "benefit" or "remittance" or "refund") Book = "welfare";
        if (Kind is "expense" or "reclassify") Book = "association";
        var code = Book == "association" ? AccountingBookCode.Association : AccountingBookCode.Welfare;
        Accounts = await (from account in database.AccountingAccounts.AsNoTracking()
            join book in database.AccountingBooks on account.BookId equals book.Id
            where book.Code == code orderby account.Code select account).ToListAsync();
        Members = Kind is "receipt" or "refund" ? await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null && (Kind != "refund" || x.Status != MemberStatus.Normal))
            .OrderBy(x => x.RunNo).ToListAsync() : [];
        AssociationAccounts = Kind == "remittance" ? await (from account in database.AccountingAccounts.AsNoTracking()
            join book in database.AccountingBooks on account.BookId equals book.Id
            where book.Code == AccountingBookCode.Association && (account.Role == AccountingAccountRole.Cash || account.Role == AccountingAccountRole.Bank)
            orderby account.Code select account).ToListAsync() : [];
        Recipients = Kind == "benefit" ? await database.DeathBeneficiarySnapshots.AsNoTracking().OrderBy(x => x.DeathCaseId).ThenBy(x => x.SlotNo).ToListAsync() : [];
        ExpenseDocuments = Kind == "reclassify" ? await (from journal in database.AccountingJournals.AsNoTracking()
            join book in database.AccountingBooks on journal.BookId equals book.Id
            where book.Code == AccountingBookCode.Association && (journal.VoucherType == "operating_expense" || journal.VoucherType == "expense_reclassification")
                && !database.AccountingJournals.Any(x => x.ReversesJournalId == journal.Id)
            orderby journal.JournalNumber descending select journal).ToListAsync() : [];
    }
}
