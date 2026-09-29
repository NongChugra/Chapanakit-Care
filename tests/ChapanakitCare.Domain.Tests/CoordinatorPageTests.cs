using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapanakitCare.Domain.Tests;

public sealed class CoordinatorPageTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient browser = null!;
    private Guid groupAMemberId;
    private Guid groupBMemberId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ChapanakitCare.Web.Pages.IndexModel).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<CoordinatorApplicationService>();
        builder.Services.AddScoped<MemberApplicationService>();
        builder.Services.AddScoped<TablePreferenceService>();
        builder.Services.AddScoped<SettingsApplicationService>();
        builder.Services.AddScoped<DemoImportService>();
        builder.Services.AddScoped<DemoDataMaintenanceService>();
        builder.Services.AddScoped<BackupApplicationService>();
        app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture("th-TH").AddSupportedCultures("th-TH").AddSupportedUICultures("th-TH"));
        app.MapRazorPages();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.EnsureCreatedAsync();
            var groupA = TestData.Member();
            groupA.GroupNo = "A";
            groupAMemberId = groupA.Id;
            var groupB = TestData.Member();
            groupB.Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
            groupB.RunNo = "00002";
            groupB.FirstName = "สมาชิก";
            groupB.LastName = "ต่างกลุ่ม";
            groupB.GroupNo = "B";
            groupBMemberId = groupB.Id;
            database.Members.AddRange(groupA, groupB);
            await database.SaveChangesAsync();
        }

        await app.StartAsync();
        browser = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer()
        })
        {
            BaseAddress = new Uri(app.Urls.Single())
        };
    }

    [Fact]
    public async Task Coordinator_route_renders_a_group_vacancy_and_its_scoped_appointment_form()
    {
        using var response = await browser.GetAsync("/Coordinators?PositionKey=group%3AA&Action=appoint");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var pageText = WebUtility.HtmlDecode(html);
        Assert.Contains("กำหนดผู้ประสานงาน", pageText);
        Assert.Contains("หัวหน้ากลุ่ม", pageText);
        Assert.Contains("กลุ่ม A", pageText);
        Assert.Contains("ยังไม่ได้แต่งตั้ง", pageText);
        Assert.Contains("ทดสอบ สมาชิก", pageText);
        Assert.DoesNotContain("สมาชิก ต่างกลุ่ม", pageText);

        var form = Assert.Single(Regex.Matches(html, @"<form\b[^>]*method=""post""[^>]*>.*?</form>", RegexOptions.Singleline)
            .Cast<Match>());
        Assert.Contains("handler=Change", form.Value);
        Assert.Contains("name=\"PositionKey\" value=\"group:A\"", form.Value);
        Assert.Contains("name=\"Action\" value=\"appoint\"", form.Value);
        Assert.Contains("name=\"ExpectedVersion\" value=\"0\"", form.Value);
        Assert.Contains("name=\"MemberId\"", form.Value);
    }

    [Fact]
    public async Task Posting_the_rendered_appointment_form_uses_PRG_and_shows_the_new_holder()
    {
        var editorHtml = await browser.GetStringAsync("/Coordinators?PositionKey=group%3AA&Action=appoint");
        var form = ChangeForm(editorHtml);
        var fields = RenderedFields(form);
        fields.Add(new("MemberId", groupAMemberId.ToString()));

        using var response = await browser.PostAsync(FormAction(form), new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Coordinators", response.Headers.Location?.OriginalString);
        var roster = WebUtility.HtmlDecode(await browser.GetStringAsync("/Coordinators"));
        Assert.Contains("ทดสอบ สมาชิก", roster);
        Assert.Contains("บันทึกการแต่งตั้งผู้รับผิดชอบแล้ว", roster);
    }

    [Fact]
    public async Task Failed_replacement_keeps_the_search_and_concurrency_context_for_a_corrected_submission()
    {
        await ChangeAsync("chairperson", "appoint", groupAMemberId, 0, null, null);
        var editorHtml = await browser.GetStringAsync("/Coordinators?PositionKey=chairperson&Action=replace&Search=00002");
        var form = ChangeForm(editorHtml);
        var fields = RenderedFields(form);
        fields.Add(new("MemberId", groupBMemberId.ToString()));

        using var response = await browser.PostAsync(FormAction(form), new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var pageText = WebUtility.HtmlDecode(html);
        Assert.Contains("กรุณาระบุเหตุผลในการเปลี่ยนหรือสิ้นสุดหน้าที่", pageText);
        Assert.Contains("name=\"Search\" value=\"00002\"", html);
        Assert.Contains("name=\"ExpectedVersion\" value=\"1\"", html);
        Assert.Contains(groupAMemberId.ToString(), html);
        Assert.Contains("สมาชิก ต่างกลุ่ม", pageText);
    }

    [Fact]
    public async Task Coordinator_roster_shows_an_immutable_history_with_member_snapshots_reason_and_Thai_date()
    {
        await ChangeAsync("chairperson", "appoint", groupAMemberId, 0, null, null);
        await ChangeAsync("chairperson", "replace", groupBMemberId, 1, groupAMemberId, "ปรับผู้รับผิดชอบตามมติ");

        var pageText = WebUtility.HtmlDecode(await browser.GetStringAsync("/Coordinators"));

        Assert.Contains("ประวัติการเปลี่ยนแปลง", pageText);
        Assert.Contains("ทดสอบ สมาชิก", pageText);
        Assert.Contains("สมาชิก ต่างกลุ่ม", pageText);
        Assert.Contains("ปรับผู้รับผิดชอบตามมติ", pageText);
        Assert.Contains(ChapanakitCare.Domain.ThaiBuddhistDate.Format(DateOnly.FromDateTime(DateTime.Today)), pageText);
    }

    [Fact]
    public async Task Library_role_filter_and_dashboard_show_the_same_holder_with_group_vacancies()
    {
        await ChangeAsync("chairperson", "appoint", groupAMemberId, 0, null, null);
        await SaveLegacyMemberPreferenceAsync();

        var libraryHtml = await browser.GetStringAsync("/Members?RoleFilter=chairperson");
        var libraryRows = Regex.Matches(libraryHtml, @"<tr\b[^>]*>.*?</tr>", RegexOptions.Singleline)
            .Cast<Match>().Where(match => match.Value.Contains("data-original-order=", StringComparison.Ordinal)).ToList();
        Assert.Single(libraryRows);
        Assert.Contains("00001", libraryRows[0].Value);
        Assert.Contains("data-column=\"role\"", libraryHtml);
        Assert.DoesNotContain("PositionKey=chairperson", WebUtility.HtmlDecode(libraryHtml));
        Assert.Contains("memberTablePreference", libraryHtml);

        var dashboard = WebUtility.HtmlDecode(await browser.GetStringAsync("/"));
        Assert.Contains("ผู้ประสานงานปัจจุบัน", dashboard);
        Assert.Contains("ทดสอบ สมาชิก", dashboard);
        Assert.Contains("กลุ่มที่ยังว่าง", dashboard);
        Assert.Contains("กลุ่ม A", dashboard);
        Assert.Contains("กลุ่ม B", dashboard);
    }

    [Fact]
    public async Task Coordinator_roster_filters_by_role_and_exact_group()
    {
        var html = await browser.GetStringAsync("/Coordinators?RoleFilter=group_leader&GroupFilter=A");
        var pageText = WebUtility.HtmlDecode(html);
        var roster = WebUtility.HtmlDecode(Regex.Match(html, @"<table class=""coordinator-table"">.*?<tbody>(.*?)</tbody>", RegexOptions.Singleline).Groups[1].Value);

        Assert.Contains("กลุ่ม A", roster);
        Assert.DoesNotContain("ทั้งสมาคม", roster);
        Assert.DoesNotContain("กลุ่ม B", roster);
        Assert.Contains("ทุกตำแหน่ง", pageText);
    }

    [Fact]
    public async Task Posting_the_rendered_replacement_form_uses_PRG_and_names_the_replacement()
    {
        await ChangeAsync("chairperson", "appoint", groupAMemberId, 0, null, null);
        var editorHtml = await browser.GetStringAsync("/Coordinators?PositionKey=chairperson&Action=replace");
        var fields = RenderedFields(ChangeForm(editorHtml));
        SetField(fields, "Reason", "เปลี่ยนตามมติที่ประชุม");
        fields.Add(new("MemberId", groupBMemberId.ToString()));

        using var response = await browser.PostAsync(FormAction(ChangeForm(editorHtml)), new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var roster = WebUtility.HtmlDecode(await browser.GetStringAsync("/Coordinators?RoleFilter=chairperson"));
        Assert.Contains("สมาชิก ต่างกลุ่ม", roster);
        Assert.Contains("บันทึกการเปลี่ยนผู้รับผิดชอบแล้ว", roster);
    }

    [Fact]
    public async Task Posting_the_rendered_end_form_uses_PRG_and_names_the_vacancy()
    {
        await ChangeAsync("chairperson", "appoint", groupAMemberId, 0, null, null);
        var editorHtml = await browser.GetStringAsync("/Coordinators?PositionKey=chairperson&Action=end");
        var fields = RenderedFields(ChangeForm(editorHtml));
        SetField(fields, "Reason", "สิ้นสุดหน้าที่ตามคำขอ");

        using var response = await browser.PostAsync(FormAction(ChangeForm(editorHtml)), new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var page = await browser.GetStringAsync("/Coordinators?RoleFilter=chairperson");
        var roster = WebUtility.HtmlDecode(Regex.Match(page, @"<table class=""coordinator-table"">.*?<tbody>(.*?)</tbody>", RegexOptions.Singleline).Groups[1].Value);
        Assert.Contains("ยังไม่ได้แต่งตั้ง", roster);
        Assert.Contains("บันทึกการสิ้นสุดหน้าที่แล้ว", WebUtility.HtmlDecode(page));
        Assert.DoesNotContain("ทดสอบ สมาชิก", roster);
    }

    [Fact]
    public async Task Coordinator_route_renders_an_enabled_navigation_link()
    {
        var html = await browser.GetStringAsync("/Coordinators");
        var navigation = Regex.Match(html, @"<nav>.*?</nav>", RegexOptions.Singleline).Value;

        Assert.Contains("href=\"/Coordinators\"", navigation);
        Assert.Contains("กำหนดผู้ประสานงาน", navigation);
    }

    [Fact]
    public async Task Submitting_a_stale_rendered_form_keeps_the_newer_holder_and_requires_reload()
    {
        await ChangeAsync("chairperson", "appoint", groupAMemberId, 0, null, null);
        var form = ChangeForm(await browser.GetStringAsync("/Coordinators?PositionKey=chairperson&Action=replace"));
        var fields = RenderedFields(form);
        SetField(fields, "Reason", "จากหน้าต่างเก่า");
        fields.Add(new("MemberId", groupBMemberId.ToString()));
        await ChangeAsync("chairperson", "replace", groupBMemberId, 1, groupAMemberId, "จากหน้าต่างใหม่");

        using var response = await browser.PostAsync(FormAction(form), new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("ข้อมูลผู้ประสานงานเปลี่ยนแปลงแล้ว", WebUtility.HtmlDecode(html));
        Assert.Contains("name=\"ExpectedVersion\" value=\"1\"", html);
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CoordinatorApplicationService>();
        Assert.Equal(groupBMemberId, (await service.GetPositionsAsync()).Single(x => x.Key == "chairperson").MemberId);
        Assert.Equal(2, (await service.GetHistoryAsync()).Count);
    }

    private Uri FormAction(string form) => new(browser.BaseAddress!, WebUtility.HtmlDecode(Attribute(form, "action")!));

    private static string ChangeForm(string html) => Assert.Single(Regex.Matches(html, @"<form\b[^>]*method=""post""[^>]*>.*?</form>", RegexOptions.Singleline)
        .Cast<Match>()).Value;

    private static List<KeyValuePair<string, string>> RenderedFields(string form)
    {
        var fields = Regex.Matches(form, @"<input\b[^>]*>", RegexOptions.Singleline)
            .Cast<Match>()
            .Where(match => Attribute(match.Value, "name") is not null && Attribute(match.Value, "type") is not "radio" and not "submit")
            .Select(match => new KeyValuePair<string, string>(
                Attribute(match.Value, "name")!, Attribute(match.Value, "value") ?? ""))
            .ToList();
        fields.AddRange(Regex.Matches(form, @"<textarea\b[^>]*>(.*?)</textarea>", RegexOptions.Singleline)
            .Cast<Match>()
            .Where(match => Attribute(match.Value, "name") is not null)
            .Select(match => new KeyValuePair<string, string>(
                Attribute(match.Value, "name")!, WebUtility.HtmlDecode(match.Groups[1].Value))));
        return fields;
    }

    private async Task ChangeAsync(string positionKey, string action, Guid? memberId, int expectedVersion, Guid? expectedMemberId, string? reason)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CoordinatorApplicationService>();
        await service.ChangeAsync(new CoordinatorChangeCommand(positionKey, action, memberId, expectedVersion, expectedMemberId, reason),
            DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, "test");
    }

    private async Task SaveLegacyMemberPreferenceAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var preferences = scope.ServiceProvider.GetRequiredService<TablePreferenceService>();
        await preferences.SaveAsync("local-user", "member-library", ["runNo", "name", "actions"], ["personalIdCard"],
            MemberTableComponents.All, "runNo", "asc", DateTimeOffset.UtcNow);
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $"\\b{name}=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static void SetField(List<KeyValuePair<string, string>> fields, string name, string value)
    {
        var index = fields.FindIndex(field => field.Key == name);
        Assert.True(index >= 0, $"Expected rendered form field {name}.");
        fields[index] = new(name, value);
    }

    public async Task DisposeAsync()
    {
        browser?.Dispose();
        if (app is not null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}
