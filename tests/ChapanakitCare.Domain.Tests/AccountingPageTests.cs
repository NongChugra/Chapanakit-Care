using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Web.Pages.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ChapanakitCare.Infrastructure.Deaths;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingPageTests
{
    [Fact]
    public async Task Voucher_reversal_form_keeps_original_and_posts_a_linked_correction()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var id = await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงิน", "receipt", [new(member.Id, 900)]), FinanceTestStore.Now, "tester");
        var page = new VoucherModel(store.Db) { ReversalDate = "12/09/2569", Reason = "รับเงินผิดคน", RequestToken = "reverse-form" };
        Assert.IsType<RedirectToPageResult>(await page.OnPostReverseAsync(id));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "1000"));
        Assert.Single(await store.Db.AccountingJournals.Where(x => x.ReversesJournalId == id).ToListAsync());
        Assert.IsType<RedirectToPageResult>(await page.OnPostReverseAsync(id));
        Assert.Equal(2, await store.Db.AccountingJournals.CountAsync());
        var invalid = new VoucherModel(store.Db) { ReversalDate = "12/09/2569", Reason = "", RequestToken = "invalid" };
        Assert.IsType<PageResult>(await invalid.OnPostReverseAsync(id));
        Assert.False(invalid.ModelState.IsValid);
    }

    [Fact]
    public async Task Fee_remittance_form_posts_both_books_without_new_income()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "ค้างนำส่ง", "open-w", [new("1000", 100, 0), new("2200", 0, 100)]), FinanceTestStore.Now, "tester");
        await service.OpenAsync(new("association", FinanceTestStore.Date, "ค้างรับ", "open-a", [new("1300", 100, 0), new("3000", 0, 100)]), FinanceTestStore.Now, "tester");
        var page = new TransactionModel(store.Db)
        { Kind = "remittance", BusinessDate = "12/09/2569", Amount = "0.60", Evidence = "นำส่งแล้ว", RequestToken = "form", MoneyAccount = "1000", TargetAccount = "1000" };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(60, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
        Assert.Equal(0, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "4000"));
    }

    [Fact]
    public async Task Benefit_payment_form_posts_against_the_chosen_beneficiary()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await store.AddMemberAsync("00001");
        var death = await new DeathApplicationService(store.Db).ConfirmAsync(new("00001", "DC-1", FinanceTestStore.Date, "เหตุ", false, null,
            new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ค้างจ่ายตามหลักฐาน", "open",
            [new("1000", 1000, 0), new("2100", 0, 1000, DeathCaseId: death.DeathCase.Id, BeneficiarySlot: 1)]), FinanceTestStore.Now, "tester");
        var page = new TransactionModel(store.Db)
        { Kind = "benefit", BusinessDate = "12/09/2569", Amount = "6.00", Evidence = "จ่ายแล้ว", RequestToken = "form", MoneyAccount = "1000", DeathCaseId = death.DeathCase.Id, BeneficiarySlot = 1 };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(400, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2100"));
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("csv", "text/csv; charset=utf-8")]
    public async Task Voucher_download_returns_the_selected_document_format(string format, string contentType)
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var id = await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงิน", "receipt", [new(member.Id, 900)]), FinanceTestStore.Now, "tester");
        var result = Assert.IsType<FileContentResult>(await new VoucherModel(store.Db).OnGetAsync(id, format));
        Assert.Equal(contentType, result.ContentType);
        Assert.EndsWith("." + format, result.FileDownloadName);
        if (format == "pdf") Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.FileContents, 0, 4));
        else Assert.Contains("W-00000001", System.Text.Encoding.UTF8.GetString(result.FileContents));
    }

    [Fact]
    public async Task Voucher_reissue_keeps_original_number_and_shows_reversed_status()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        var service = new FinanceOperationService(store.Db);
        await service.OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var id = await service.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงิน", "receipt", [new(member.Id, 900)]), FinanceTestStore.Now, "tester");
        var original = new VoucherModel(store.Db);
        Assert.IsType<PageResult>(await original.OnGetAsync(id));
        Assert.Equal("ORIGINAL", original.DocumentStatus);
        var number = original.Voucher!.JournalNumber;
        await service.ReverseAsync(new(id, FinanceTestStore.Date, "คืนเงินตามหลักฐาน", "reverse"), FinanceTestStore.Now, "tester");
        var reissue = new VoucherModel(store.Db);
        Assert.IsType<PageResult>(await reissue.OnGetAsync(id));
        Assert.Equal("REVERSED", reissue.DocumentStatus);
        Assert.Equal(number, reissue.Voucher!.JournalNumber);
        Assert.IsType<NotFoundResult>(await new VoucherModel(store.Db).OnGetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Expense_form_lists_only_association_accounts()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new ChapanakitCare.Infrastructure.Accounting.AccountingSetupService(store.Db).EnsureCatalogAsync();
        var page = new TransactionModel(store.Db);
        await page.OnGetAsync("expense");
        var book = await store.Db.AccountingBooks.SingleAsync(x => x.Code == AccountingBookCode.Association);
        Assert.Equal("association", page.Book);
        Assert.NotEmpty(page.Accounts);
        Assert.All(page.Accounts, account => Assert.Equal(book.Id, account.BookId));
    }

    [Theory]
    [InlineData("expense", "5000", 2500, 7500)]
    [InlineData("transfer", "1100", 2500, 7500)]
    public async Task Operating_form_posts_selected_expense_or_bank_transfer(string kind, string target, long expectedTarget, long expectedCash)
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("association", FinanceTestStore.Date, "ยอดเปิด", "open",
            [new("1000", 10000, 0), new("3000", 0, 10000)]), FinanceTestStore.Now, "tester");
        var page = new TransactionModel(store.Db)
        {
            Kind = kind, BusinessDate = "12/09/2569", Amount = "25.00", Evidence = "หลักฐาน", RequestToken = "form",
            Book = "association", MoneyAccount = "1000", ExpenseAccount = "5000", TargetAccount = "1100", Payee = "ร้านค้า"
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal(expectedTarget, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, target));
        Assert.Equal(expectedCash, await FinanceOperationTests.Balance(store, AccountingBookCode.Association, "1000"));
    }

    [Fact]
    public async Task Receipt_form_converts_buddhist_date_and_baht_then_redirects_to_saved_voucher()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var page = new TransactionModel(store.Db)
        {
            Kind = "receipt", BusinessDate = "12/09/2569", Amount = "9.01", Evidence = "รับจากสมาชิก", RequestToken = "form",
            MemberId = member.Id, Book = "welfare", MoneyAccount = "1000"
        };
        var result = Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());
        Assert.Equal("./Voucher", result.PageName);
        var journal = await store.Db.AccountingJournals.SingleAsync();
        Assert.Equal(FinanceTestStore.Date, journal.BusinessDate);
        Assert.Equal(journal.Id, result.RouteValues!["id"]);
        Assert.Equal(901, await FinanceOperationTests.Balance(store, AccountingBookCode.Welfare, "2000"));
    }

    [Fact]
    public async Task Receipt_form_rejects_subsatang_input_without_posting()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var page = new TransactionModel(store.Db)
        {
            Kind = "receipt", BusinessDate = "12/09/2569", Amount = "9.001", Evidence = "รับเงิน", RequestToken = "form", MemberId = Guid.NewGuid()
        };
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }
}
