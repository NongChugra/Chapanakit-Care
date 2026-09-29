using System.Text;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Web.Pages.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingReportPageTests
{
    private static readonly DateOnly OpeningDate = new(2026, 9, 1);
    private static readonly DateOnly BeforeCutoffDate = new(2026, 9, 10);
    private static readonly DateOnly AfterCutoffDate = new(2026, 9, 11);

    [Fact]
    public async Task Trial_balance_page_uses_the_selected_book_and_business_date_cutoff()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var journalsBefore = await store.Context.AccountingJournals.CountAsync();
        var linesBefore = await store.Context.AccountingJournalLines.CountAsync();
        var page = new ReportsModel(store.Context)
        {
            Report = "trialbalance",
            Book = "welfare",
            AsOf = "10/09/2569"
        };

        Assert.IsType<PageResult>(await page.OnGetAsync());

        Assert.NotNull(page.TrialBalance);
        Assert.Equal(AccountingBookCode.Welfare, page.TrialBalance!.BookCode);
        Assert.Equal(BeforeCutoffDate, page.TrialBalance.AsOfDate);
        Assert.Equal(100, Assert.Single(page.TrialBalance.Rows, row => row.AccountCode == "1000").DebitBalanceSatang);
        Assert.DoesNotContain(page.TrialBalance.Rows, row => row.AccountCode == "4000");
        Assert.Equal(journalsBefore, await store.Context.AccountingJournals.CountAsync());
        Assert.Equal(linesBefore, await store.Context.AccountingJournalLines.CountAsync());
    }

    [Fact]
    public async Task General_ledger_page_scopes_account_choices_to_the_selected_book_and_parses_a_period()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var association = await store.Context.AccountingBooks.SingleAsync(book => book.Code == AccountingBookCode.Association);
        var page = new ReportsModel(store.Context)
        {
            Report = "generalLedger",
            Book = "association",
            AccountCode = "1000",
            From = "10/09/2569",
            To = "11/09/2569"
        };

        Assert.IsType<PageResult>(await page.OnGetAsync());

        Assert.NotNull(page.GeneralLedger);
        Assert.Equal(AccountingBookCode.Association, page.GeneralLedger!.BookCode);
        Assert.Equal(new AccountingReportPeriod(BeforeCutoffDate, AfterCutoffDate), page.GeneralLedger.Period);
        Assert.NotEmpty(page.Accounts);
        Assert.All(page.Accounts, account => Assert.Equal(association.Id, account.BookId));
        Assert.Contains(page.Accounts, account => account.Code == "1000");
    }

    [Fact]
    public async Task Report_page_rejects_unknown_book_and_malformed_buddhist_date_without_falling_back()
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var journalsBefore = await store.Context.AccountingJournals.CountAsync();
        var page = new ReportsModel(store.Context)
        {
            Report = "trialbalance",
            Book = "other-book",
            AsOf = "31/02/2569"
        };

        Assert.IsType<PageResult>(await page.OnGetAsync());

        Assert.False(page.ModelState.IsValid);
        Assert.Null(page.TrialBalance);
        Assert.Contains(page.ModelState.Values.SelectMany(value => value.Errors), error =>
            error.ErrorMessage?.Contains("สมุดบัญชี", StringComparison.Ordinal) == true);
        Assert.Equal(journalsBefore, await store.Context.AccountingJournals.CountAsync());
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("csv", "text/csv; charset=utf-8")]
    public async Task Report_page_returns_the_requested_read_only_document_format(string format, string contentType)
    {
        await using var store = await AccountingTestDatabase.CreateAsync();
        await SeedAsync(store.Context);
        var page = new ReportsModel(store.Context)
        {
            Report = "trialbalance",
            Book = "association",
            AsOf = "10/09/2569"
        };

        var result = Assert.IsType<FileContentResult>(await page.OnGetAsync(format));

        Assert.Equal(contentType, result.ContentType);
        Assert.EndsWith("." + format, result.FileDownloadName, StringComparison.Ordinal);
        if (format == "pdf")
            Assert.Equal("%PDF", Encoding.ASCII.GetString(result.FileContents, 0, 4));
        else
            Assert.Contains("งบทดลอง", Encoding.UTF8.GetString(result.FileContents), StringComparison.Ordinal);
    }

    private static async Task SeedAsync(ChapanakitCare.Infrastructure.Persistence.AppDbContext database)
    {
        var setup = new AccountingSetupService(database);
        await setup.EnsureCatalogAsync();
        await setup.ActivateBookAsync(new(AccountingBookCode.Welfare, OpeningDate, "ยอดยกมาตามหลักฐาน", Actor()));
        await setup.ActivateBookAsync(new(AccountingBookCode.Association, OpeningDate, "ยอดยกมาตามหลักฐาน", Actor()));

        var posting = new AccountingPostingService(database);
        await PostAsync(posting, AccountingBookCode.Welfare, OpeningDate, "w-opening", [
            new("1000", 100, 0),
            new("2000", 0, 100)
        ]);
        await PostAsync(posting, AccountingBookCode.Welfare, AfterCutoffDate, "w-after-cutoff", [
            new("1000", 200, 0),
            new("2000", 0, 200)
        ]);
        await PostAsync(posting, AccountingBookCode.Association, OpeningDate, "a-opening", [
            new("1000", 500, 0),
            new("3000", 0, 500)
        ]);
        await PostAsync(posting, AccountingBookCode.Association, AfterCutoffDate, "a-after-cutoff", [
            new("1000", 600, 0),
            new("3000", 0, 600)
        ]);
    }

    private static Task<AccountingPostingResult> PostAsync(
        AccountingPostingService posting,
        AccountingBookCode bookCode,
        DateOnly date,
        string token,
        IReadOnlyList<AccountingPostLine> lines) =>
        posting.PostAsync(new(
            bookCode,
            "opening",
            "รายงานทดสอบ",
            date,
            token,
            token + "-fingerprint",
            Actor(),
            lines));

    private static AccountingActor Actor() => new("report-page-test", "ผู้ทดสอบรายงาน", "TEST-PC", "test");
}
