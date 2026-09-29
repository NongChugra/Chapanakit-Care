using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapanakitCare.Domain.Tests;

public sealed class ReportFormSubmissionTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ChapanakitCare.Web.Pages.Reports.IndexModel).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<ReportApplicationService>();
        app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture("th-TH").AddSupportedCultures("th-TH").AddSupportedUICultures("th-TH"));
        app.MapRazorPages();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.EnsureCreatedAsync();
            var member = TestData.Member();
            member.GroupNo = "A";
            database.Members.Add(member);
            await database.SaveChangesAsync();
        }

        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [Theory]
    [InlineData("รายงานสมาชิกทั้งหมด", "all-members")]
    [InlineData("รายงานสมาชิกแยกตามกลุ่ม", "members-group-A")]
    [InlineData("รายงานสมาชิกประจำเดือน", "monthly-members")]
    [InlineData("แบบ ส.ฌ.ก.๑", "sak-one")]
    public async Task Submitting_the_rendered_report_form_downloads_the_selected_pdf(string title, string filePrefix)
    {
        var html = await client.GetStringAsync("/Reports");
        var article = Assert.Single(Regex.Matches(html, @"<article\b[^>]*>.*?</article>", RegexOptions.Singleline)
            .Cast<Match>(), match => WebUtility.HtmlDecode(match.Value).Contains($"<h2>{title}</h2>")).Value;
        var form = Regex.Match(article, @"<form\b[^>]*>").Value;
        Assert.Equal("get", Attribute(form, "method"));

        // Submit the rendered controls, just as a browser does. GET submission
        // replaces the action's query string; it does not merge it with the fields.
        var fields = Regex.Matches(article, @"<input\b[^>]*>").Cast<Match>()
            .Where(match => Attribute(match.Value, "name") is not null)
            .Select(match => new KeyValuePair<string, string>(
                Attribute(match.Value, "name")!, Attribute(match.Value, "value") ?? "")).ToList();
        var select = Regex.Match(article, @"<select\b[^>]*>.*?</select>", RegexOptions.Singleline);
        if (select.Success)
        {
            var option = Regex.Matches(select.Value, @"<option\b[^>]*>").Cast<Match>()
                .First(match => !string.IsNullOrEmpty(Attribute(match.Value, "value")));
            fields.Add(new(Attribute(select.Value, "name")!, Attribute(option.Value, "value")!));
        }
        using var encodedFields = new FormUrlEncodedContent(fields);
        var target = new UriBuilder(new Uri(client.BaseAddress!, Attribute(form, "action")!))
        {
            Query = await encodedFields.ReadAsStringAsync()
        };

        using var response = await client.GetAsync(target.Uri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.Equal("attachment", disposition?.DispositionType);
        Assert.StartsWith(filePrefix, disposition!.FileNameStar ?? disposition.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
        Assert.Contains("ทดสอบ สมาชิก", ReportPdfText.Extract(bytes));
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $"\\b{name}=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}
