using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ChapanakitCare.Infrastructure.AccountingReports;
using Microsoft.EntityFrameworkCore;
using ChapanakitCare.Domain;
using ChapanakitCare.Infrastructure.Reports;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed class VoucherModel : PageModel
{
    private readonly AppDbContext database;
    public VoucherModel(AppDbContext database) { this.database = database; }
    public AccountingJournal? Voucher { get; private set; }
    public string DocumentStatus { get; private set; } = "";
    public string BookName { get; private set; } = "";
    public IReadOnlyList<AccountingJournalLineReport> Lines { get; private set; } = [];
    public Guid? ReversalId { get; private set; }
    [BindProperty] public string ReversalDate { get; set; } = "";
    [BindProperty] public string Reason { get; set; } = "";
    [BindProperty] public string RequestToken { get; set; } = "";
    public async Task<IActionResult> OnPostReverseAsync(Guid id)
    {
        if (!ModelState.IsValid) return await OnGetAsync(id);
        try
        {
            var reversal = await new FinanceOperationService(database).ReverseAsync(new(id,
                ThaiBuddhistDate.Parse(ReversalDate), Reason, RequestToken), DateTimeOffset.UtcNow, "ผู้ใช้งานเครื่องนี้");
            return RedirectToPage(new { id = reversal });
        }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException or AccountingValidationException or AccountingIdempotencyConflictException or AccountingPeriodClosedException)
        {
            ModelState.AddModelError(string.Empty, error is FormatException or ArgumentException or OverflowException
                ? "กรุณาตรวจสอบวันที่กลับรายการ (พ.ศ.)" : error.Message);
            return await OnGetAsync(id);
        }
    }
    public async Task<IActionResult> OnGetAsync(Guid id, string? format = null)
    {
        if (string.IsNullOrEmpty(ReversalDate)) ReversalDate = ThaiBuddhistDate.Format(DateOnly.FromDateTime(DateTime.Today));
        if (string.IsNullOrEmpty(RequestToken)) RequestToken = Guid.NewGuid().ToString("N");
        Voucher = await database.AccountingJournals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (Voucher is null) return NotFound();
        BookName = await database.AccountingBooks.Where(x => x.Id == Voucher.BookId).Select(x => x.Name).SingleAsync();
        ReversalId = await database.AccountingJournals.Where(x => x.ReversesJournalId == id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
        DocumentStatus = ReversalId is null ? "ORIGINAL" : "REVERSED";
        Lines = await (from line in database.AccountingJournalLines.AsNoTracking()
            join account in database.AccountingAccounts on line.AccountId equals account.Id
            where line.JournalId == id orderby line.LineNo
            select new AccountingJournalLineReport(line.LineNo, account.Code, account.Name, line.DebitSatang, line.CreditSatang,
                line.MemberId, line.DeathCaseId, line.BeneficiarySlotNo, line.PartySnapshot, line.DescriptionSnapshot)).ToListAsync();
        if (format is "pdf" or "csv")
        {
            var rows = Lines.Select(line => (IReadOnlyList<string>)new[] { line.AccountCode, line.AccountName, line.PartySnapshot ?? "",
                FinanceMoney.Format(line.DebitSatang), FinanceMoney.Format(line.CreditSatang) }).ToList();
            rows.Add(["", "รวม", "", FinanceMoney.Format(Lines.Sum(x => x.DebitSatang)), FinanceMoney.Format(Lines.Sum(x => x.CreditSatang))]);
            var document = new AccountingDocumentData("ใบสำคัญรายการบัญชี", BookName, null, null, Voucher.BusinessDate,
                [new("รหัสบัญชี"), new("ชื่อบัญชี", RelativeWidth: 2), new("สมาชิก / ผู้รับเงิน", RelativeWidth: 2),
                 new("เดบิต (บาท)", AccountingDocumentColumnAlignment.Right, Type: AccountingDocumentColumnType.Numeric),
                 new("เครดิต (บาท)", AccountingDocumentColumnAlignment.Right, Type: AccountingDocumentColumnType.Numeric)],
                rows, [Voucher.DescriptionSnapshot, "ผู้บันทึก: " + Voucher.ActorDisplayName], Voucher.JournalNumber, DocumentStatus,
                ["ผู้จัดทำ", "ผู้ตรวจสอบ"]);
            return format == "pdf" ? File(AccountingDocumentRenderer.GeneratePdf(document), "application/pdf", Voucher.JournalNumber + ".pdf")
                : File(AccountingDocumentRenderer.GenerateCsv(document), "text/csv; charset=utf-8", Voucher.JournalNumber + ".csv");
        }
        if (format is not null) return BadRequest("กรุณาเลือกไฟล์ PDF หรือ CSV");
        return Page();
    }
}
