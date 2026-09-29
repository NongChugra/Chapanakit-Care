using System.Net;
using System.Text.RegularExpressions;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingHttpFormTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient client = null!;
    private Guid memberId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { ApplicationName = typeof(ChapanakitCare.Web.Pages.Accounting.TransactionModel).Assembly.GetName().Name });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("th-TH").AddSupportedCultures("th-TH").AddSupportedUICultures("th-TH"));
        app.MapRazorPages();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var member = TestData.Member();
            memberId = member.Id;
            db.Members.Add(member);
            await db.SaveChangesAsync();
            await new FinanceOperationService(db).OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        }
        await app.StartAsync();
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [Theory]
    [InlineData("/Accounting/Reports?Report=trialbalance&Book=welfare&AsOf=12%2F09%2F2569")]
    [InlineData("/Accounting/Reports")]
    public async Task Trial_report_browser_request_shows_no_errors_for_irrelevant_filters(string path)
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(path));
        Assert.DoesNotContain("validation-summary-errors", html);
        Assert.Contains("งบทดลอง", html);
    }

    [Theory]
    [InlineData("/Accounting")]
    [InlineData("/Accounting/Opening")]
    [InlineData("/Accounting/Opening?Book=association")]
    [InlineData("/Accounting/Manage")]
    [InlineData("/Accounting/Transaction?kind=receipt")]
    [InlineData("/Accounting/Transaction?kind=reclassify")]
    public async Task Initial_accounting_pages_render_without_premature_validation_errors(string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.False(html.Contains("validation-summary-errors"), Regex.Match(html, "<div[^>]*validation-summary-errors[\\s\\S]*?</div>").Value);
    }

    [Fact]
    public async Task Receipt_browser_form_accepts_only_its_visible_fields_and_posts_once()
    {
        var html = await client.GetStringAsync("/Accounting/Transaction?kind=receipt");
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken"),
            ["RequestToken"] = Input(html, "RequestToken"), ["Kind"] = "receipt", ["Book"] = "welfare",
            ["BusinessDate"] = "12/09/2569", ["Amount"] = "9.01", ["MoneyAccount"] = "1000",
            ["MemberId"] = memberId.ToString(), ["Evidence"] = "รับเงินจริง"
        };
        using var response = await client.PostAsync("/Accounting/Transaction", new FormUrlEncodedContent(fields));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.AccountingJournals.ToListAsync());
        Assert.Contains(await db.AccountingJournalLines.ToListAsync(), x => x.DebitSatang == 901);
    }

    [Fact]
    public async Task Bank_browser_form_does_not_require_fields_from_other_management_forms()
    {
        var html = await client.GetStringAsync("/Accounting/Manage?Book=welfare");
        using var response = await client.PostAsync("/Accounting/Manage", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken"), ["Book"] = "welfare", ["Action"] = "bank",
            ["AccountCode"] = "1101", ["AccountName"] = "ธนาคารสมาชิก", ["BankDescription"] = "อ้างอิง 123"
        }));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        await using var scope = app.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AccountingAccounts.AnyAsync(x => x.Code == "1101"));
    }

    private static string Input(string html, string name) => WebUtility.HtmlDecode(Regex.Match(
        Regex.Matches(html, "<input\\b[^>]*>").Cast<Match>().First(x => x.Value.Contains($"name=\"{name}\"", StringComparison.Ordinal)).Value,
        "value=\"([^\"]*)\"").Groups[1].Value);

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}
