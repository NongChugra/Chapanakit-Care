using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Web.Pages.Accounting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingCollectionPageTests
{
    [Fact]
    public async Task Collection_page_creates_selected_normal_members_without_recording_cash()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var selected = await store.AddMemberAsync("00001", "07");
        var unselected = await store.AddMemberAsync("00002", "07");
        var page = new CollectionsModel(store.Db)
        {
            Action = "create",
            GroupNo = "07",
            RequestCycleKey = "2569-09",
            RequestBusinessDate = "12/09/2569",
            RequestDueDate = "30/09/2569",
            RequestDescription = "เรียกเก็บเงินสงเคราะห์",
            RequestToken = "collection-create",
            RequestLines =
            [
                new() { MemberId = selected.Id, Selected = true, Amount = "9.01" },
                new() { MemberId = unselected.Id, Selected = false, Amount = "9.01" }
            ]
        };

        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());

        var request = Assert.Single(await store.Db.Set<WelfareCollection>().ToListAsync());
        Assert.Equal(selected.Id, request.MemberId);
        Assert.Equal(901, request.AmountSatang);
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
        Assert.Empty(await store.Db.AccountingJournalLines.ToListAsync());
    }

    [Fact]
    public async Task Collection_page_posts_selected_receipt_allocation_without_marking_the_request_paid_by_itself()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001", "07");
        var request = Assert.Single(await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(18), "เรียกเก็บเงินสงเคราะห์",
            "collection-request", [new(member.Id, 1_000)]), FinanceTestStore.Now, "tester"));
        await new FinanceOperationService(store.Db).OpenAsync(new(
            "welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open-welfare", []), FinanceTestStore.Now, "tester");
        var page = new CollectionsModel(store.Db)
        {
            Action = "receipt",
            ReceiptDate = "12/09/2569",
            ReceiptMoneyAccount = "1000",
            ReceiptDescription = "รับเงินจริงจากผู้ประสานงาน",
            ReceiptToken = "collection-receipt",
            ReceiptLines = [new() { CollectionId = request.Id, MemberId = member.Id, Selected = true, Amount = "4.00" }]
        };

        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());

        var savedRequest = await store.Db.Set<WelfareCollection>().SingleAsync();
        var report = await new CollectionReportService(store.Db).GetBalancesAsync(FinanceTestStore.Date);
        Assert.Equal(1_000, savedRequest.AmountSatang);
        Assert.Equal(400, Assert.Single(report.Rows).PaidSatang);
        Assert.Equal(600, Assert.Single(report.Rows).OutstandingSatang);
    }

    [Fact]
    public async Task Collection_page_displays_an_error_for_an_overpayment_without_writing_a_journal()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001", "07");
        var request = Assert.Single(await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(18), "เรียกเก็บเงินสงเคราะห์",
            "collection-request", [new(member.Id, 1_000)]), FinanceTestStore.Now, "tester"));
        await new FinanceOperationService(store.Db).OpenAsync(new(
            "welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open-welfare", []), FinanceTestStore.Now, "tester");
        var page = new CollectionsModel(store.Db)
        {
            Action = "receipt",
            ReceiptDate = "12/09/2569",
            ReceiptMoneyAccount = "1000",
            ReceiptDescription = "รับเงินจริงเกินยอด",
            ReceiptToken = "collection-overpayment",
            ReceiptLines = [new() { CollectionId = request.Id, MemberId = member.Id, Selected = true, Amount = "10.01" }]
        };

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostAsync());

        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("csv", "text/csv; charset=utf-8")]
    public async Task Collection_page_exports_a_read_only_due_reminder(string format, string contentType)
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001", "07");
        await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date, "เรียกเก็บเงินสงเคราะห์",
            "collection-reminder", [new(member.Id, 1_000)]), FinanceTestStore.Now, "tester");
        var journalsBefore = await store.Db.AccountingJournals.CountAsync();
        var page = new CollectionsModel(store.Db)
        {
            AsOf = "12/09/2569",
            GroupNo = "07"
        };

        var result = Assert.IsType<FileContentResult>(await page.OnGetAsync(format, "reminders"));

        Assert.Equal(contentType, result.ContentType);
        Assert.EndsWith("." + format, result.FileDownloadName, StringComparison.Ordinal);
        if (format == "pdf")
            Assert.Equal("%PDF", Encoding.ASCII.GetString(result.FileContents, 0, 4));
        else
            Assert.Contains("รายการแจ้งเตือนยอดค้างชำระ", Encoding.UTF8.GetString(result.FileContents), StringComparison.Ordinal);
        Assert.Equal(journalsBefore, await store.Db.AccountingJournals.CountAsync());
    }
}

public sealed class AccountingCollectionHttpFormTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient client = null!;
    private Guid memberId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CollectionsModel).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        app = builder.Build();
        app.MapRazorPages();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.EnsureCreatedAsync();
            var member = TestData.Member();
            member.GroupNo = "07";
            memberId = member.Id;
            database.Members.Add(member);
            await database.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [Fact]
    public async Task Collection_browser_form_accepts_only_its_visible_create_fields()
    {
        var html = await client.GetStringAsync("/Accounting/Collections?GroupNo=07");
        using var response = await client.PostAsync("/Accounting/Collections", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken"),
            ["Action"] = "create", ["GroupNo"] = "07", ["RequestToken"] = Input(html, "RequestToken"),
            ["RequestCycleKey"] = "2569-09", ["RequestBusinessDate"] = "12/09/2569", ["RequestDueDate"] = "30/09/2569",
            ["RequestDescription"] = "เรียกเก็บเงินสงเคราะห์", ["RequestLines[0].MemberId"] = memberId.ToString(),
            ["RequestLines[0].Selected"] = "true", ["RequestLines[0].Amount"] = "9.00"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await database.Set<WelfareCollection>().ToListAsync());
        Assert.Empty(await database.AccountingJournals.ToListAsync());
    }

    private static string Input(string html, string name) => System.Net.WebUtility.HtmlDecode(Regex.Match(
        Regex.Matches(html, "<input\\b[^>]*>").Cast<Match>().First(match => match.Value.Contains($"name=\"{name}\"", StringComparison.Ordinal)).Value,
        "value=\"([^\"]*)\"").Groups[1].Value);

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}
