using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed class OpeningMemberInput
{
    public Guid? MemberId { get; set; }
    public string Advance { get; set; } = "0";
    public string Shortfall { get; set; } = "0";
}

public sealed class OpeningModel : PageModel
{
    private readonly AppDbContext database;
    public OpeningModel(AppDbContext database) { this.database = database; }
    [BindProperty(SupportsGet = true)] public string Book { get; set; } = "welfare";
    [BindProperty] public string BusinessDate { get; set; } = "";
    [BindProperty] public string Cash { get; set; } = "0";
    [BindProperty] public string Bank { get; set; } = "0";
    [BindProperty] public string FeeBalance { get; set; } = "0";
    [BindProperty] public string AccumulatedFund { get; set; } = "0";
    [BindProperty] public string Evidence { get; set; } = "";
    [BindProperty] public string RequestToken { get; set; } = "";
    [BindProperty] public bool ZeroConfirmed { get; set; }
    [BindProperty] public List<OpeningMemberInput> MemberBalances { get; set; } = [];
    [BindProperty] public List<OpeningMoneyInput> AdditionalMoney { get; set; } = [];
    [BindProperty] public List<OpeningPayableInput> BenefitBalances { get; set; } = [];
    public IReadOnlyList<Member> Members { get; private set; } = [];
    public IReadOnlyList<AccountingAccount> MoneyAccounts { get; private set; } = [];
    public IReadOnlyList<DeathBeneficiarySnapshot> Recipients { get; private set; } = [];
    public bool AlreadyActive { get; private set; }

    public async Task OnGetAsync()
    {
        if (Book is "welfare" or "association") ModelState.Remove(nameof(Book));
        BusinessDate = ThaiBuddhistDate.Format(DateOnly.FromDateTime(DateTime.Today));
        RequestToken = Guid.NewGuid().ToString("N");
        MemberBalances = Enumerable.Range(0, 8).Select(_ => new OpeningMemberInput()).ToList();
        AdditionalMoney = [new()];
        BenefitBalances = [new()];
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        try
        {
            if (Book is not ("welfare" or "association")) throw new AccountingValidationException("กรุณาเลือกสมุดบัญชี");
            var lines = new List<OpeningAccountAmount>();
            void Add(string account, long amount, bool debit, Guid? memberId = null)
            {
                if (amount > 0) lines.Add(new(account, debit ? amount : 0, debit ? 0 : amount, memberId));
            }
            Add("1000", FinanceMoney.Parse(Cash, true), true);
            Add("1100", FinanceMoney.Parse(Bank, true), true);
            foreach (var extra in AdditionalMoney)
            {
                var amount = FinanceMoney.Parse(extra.Amount, true);
                if (amount == 0) continue;
                var code = Book == "welfare" ? AccountingBookCode.Welfare : AccountingBookCode.Association;
                if (extra.AccountCode is "1000" or "1100" || !await (from account in database.AccountingAccounts
                    join book in database.AccountingBooks on account.BookId equals book.Id
                    where book.Code == code && account.Code == extra.AccountCode
                        && (account.Role == AccountingAccountRole.Cash || account.Role == AccountingAccountRole.Bank)
                    select account).AnyAsync())
                    throw new AccountingValidationException("กรุณาเลือกบัญชีเงินเพิ่มเติมที่อยู่ในสมุดนี้ และไม่ซ้ำช่องเงินสด/ธนาคารหลัก");
                Add(extra.AccountCode, amount, true);
            }
            foreach (var payable in BenefitBalances)
            {
                var amount = FinanceMoney.Parse(payable.Amount, true);
                if (amount == 0) continue;
                if (Book != "welfare" || payable.DeathCaseId is null)
                    throw new AccountingValidationException("ยอดค้างจ่ายผู้รับผลประโยชน์ต้องระบุเหตุเสียชีวิตในสมุดเงินสงเคราะห์");
                lines.Add(new("2100", 0, amount, DeathCaseId: payable.DeathCaseId, BeneficiarySlot: payable.BeneficiarySlot));
            }
            Add(Book == "welfare" ? "2200" : "1300", FinanceMoney.Parse(FeeBalance, true), Book == "association");
            var negativeFund = AccumulatedFund.Trim().StartsWith('-');
            var fund = FinanceMoney.Parse(negativeFund ? AccumulatedFund.Trim()[1..] : AccumulatedFund, true);
            Add("3000", fund, negativeFund);
            foreach (var member in MemberBalances)
            {
                var advance = FinanceMoney.Parse(member.Advance, true);
                var debt = FinanceMoney.Parse(member.Shortfall, true);
                if (advance == 0 && debt == 0) continue;
                if (Book != "welfare" || member.MemberId is null || advance > 0 && debt > 0)
                    throw new AccountingValidationException("ยอดสมาชิกใช้เฉพาะสมุดเงินสงเคราะห์ และต้องระบุสมาชิกพร้อมยอดล่วงหน้าหรือยอดค้างเพียงด้านเดียว");
                Add("2000", advance, false, member.MemberId);
                Add("1200", debt, true, member.MemberId);
            }
            if (lines.Count == 0 && !ZeroConfirmed)
                throw new AccountingValidationException("กรุณายืนยันว่ายอดเริ่มต้นเป็นศูนย์ตามหลักฐาน");
            var id = await new FinanceOperationService(database).OpenAsync(new(Book, ThaiBuddhistDate.Parse(BusinessDate), Evidence,
                RequestToken, lines), DateTimeOffset.UtcNow, "ผู้ใช้งานเครื่องนี้");
            return id == Guid.Empty ? RedirectToPage("./Index") : RedirectToPage("./Voucher", new { id });
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
        var code = Book == "association" ? AccountingBookCode.Association : AccountingBookCode.Welfare;
        AlreadyActive = await database.AccountingBooks.AnyAsync(x => x.Code == code && x.IsActivated);
        Members = await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null).OrderBy(x => x.RunNo).ToListAsync();
        MoneyAccounts = await (from account in database.AccountingAccounts.AsNoTracking()
            join book in database.AccountingBooks on account.BookId equals book.Id
            where book.Code == code && account.Code != "1000" && account.Code != "1100"
                && (account.Role == AccountingAccountRole.Cash || account.Role == AccountingAccountRole.Bank)
            orderby account.Code select account).ToListAsync();
        Recipients = Book == "welfare" ? await database.DeathBeneficiarySnapshots.AsNoTracking().OrderBy(x => x.DeathCaseId).ThenBy(x => x.SlotNo).ToListAsync() : [];
    }
}

public sealed class OpeningMoneyInput
{
    public string AccountCode { get; set; } = "1101";
    public string Amount { get; set; } = "0";
}

public sealed class OpeningPayableInput
{
    public Guid? DeathCaseId { get; set; }
    public int BeneficiarySlot { get; set; } = 1;
    public string Amount { get; set; } = "0";
}
