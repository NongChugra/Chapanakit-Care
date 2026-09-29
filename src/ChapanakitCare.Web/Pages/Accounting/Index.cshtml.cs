using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Accounting;

public sealed record AccountingBookOverview(AccountingBook Book, IReadOnlyList<AccountingTrialBalanceRow> CashAccounts,
    long AdvanceSatang, long DueSatang, long IncomeSatang, long ExpenseSatang);

public sealed class IndexModel(AppDbContext database) : PageModel
{
    public DateOnly AsOf { get; } = DateOnly.FromDateTime(DateTime.Today);
    public IReadOnlyList<AccountingBookOverview> Books { get; private set; } = [];
    public IReadOnlyList<AccountingJournal> Recent { get; private set; } = [];
    public async Task OnGetAsync()
    {
        var books = await database.AccountingBooks.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        var overviews = new List<AccountingBookOverview>();
        var reports = new AccountingReportQueryService(database);
        foreach (var book in books)
        {
            var report = await reports.GetTrialBalanceAsync(book.Code, AsOf);
            var moneyCodes = await database.AccountingAccounts.Where(x => x.BookId == book.Id
                && (x.Role == AccountingAccountRole.Cash || x.Role == AccountingAccountRole.Bank)).Select(x => x.Code).ToListAsync();
            long Credit(string code) => report.Rows.Where(x => x.AccountCode == code).Sum(x => x.CreditBalanceSatang - x.DebitBalanceSatang);
            overviews.Add(new(book, report.Rows.Where(x => moneyCodes.Contains(x.AccountCode)).ToList(), Credit("2000"),
                book.Code == AccountingBookCode.Welfare ? Credit("2200") : -Credit("1300"), Credit("4000"),
                report.Rows.Where(x => x.AccountType == AccountingAccountType.Expense).Sum(x => x.DebitBalanceSatang - x.CreditBalanceSatang)));
        }
        Books = overviews;
        // SQLite cannot order DateTimeOffset values; compare their instants in .NET.
        Recent = (await database.AccountingJournals.AsNoTracking().ToListAsync())
            .OrderByDescending(x => x.RecordedAtUtc).Take(30).ToList();
    }
}
