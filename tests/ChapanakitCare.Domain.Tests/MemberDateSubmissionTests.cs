using System.Net;
using System.Text.RegularExpressions;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChapanakitCare.Infrastructure.Deaths;

namespace ChapanakitCare.Domain.Tests;

public sealed class MemberDateSubmissionTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ChapanakitCare.Web.Pages.Members.CreateModel).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<MemberApplicationService>();
        builder.Services.AddScoped<DeathApplicationService>();
        builder.Services.AddScoped<TablePreferenceService>();
        app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture("th-TH").AddSupportedCultures("th-TH").AddSupportedUICultures("th-TH"));
        app.MapRazorPages();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.NumberSequences.Add(new NumberSequence { SequenceKey = "member_run_no", NextValue = 1, Width = 5 });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [Fact]
    public async Task Invalid_id_redisplay_and_corrected_submission_preserve_gregorian_dates()
    {
        var html = await client.GetStringAsync("/Members/Create");
        var fields = Fields();
        fields["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken");
        fields["Input.PersonalIdCard"] = "123";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var invalid = await client.PostAsync("/Members/Create", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            html = await invalid.Content.ReadAsStringAsync();
            Assert.Contains("13", WebUtility.HtmlDecode(html));
            Assert.Equal("2004-07-19", InputValue(html, "Input.BirthDate"));
            Assert.Equal("2026-09-08", InputValue(html, "Input.ApplicationDate"));
            Assert.Equal("2026-09-08", InputValue(html, "Input.ApprovalDate"));
            fields["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken");
        }
        fields["Input.PersonalIdCard"] = "1234567890123";
        using var response = await client.PostAsync("/Members/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var member = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members.SingleAsync();
        Assert.Equal(new DateOnly(2004, 7, 19), member.BirthDate);
        Assert.Equal(new DateOnly(2026, 9, 8), member.ApplicationDate);
        Assert.Equal(new DateOnly(2027, 3, 7), member.CoverageStartDate);
    }

    [Fact]
    public async Task Same_address_checkbox_copies_member_address_on_server()
    {
        var html = await client.GetStringAsync("/Members/Create");
        var fields = Fields();
        fields["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken");
        fields["Input.Beneficiary1.UseMemberAddress"] = "true";
        using var response = await client.PostAsync("/Members/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var beneficiary = await db.MemberBeneficiaries.SingleAsync();
        Assert.Equal("11", beneficiary.HouseNo);
        Assert.Equal("บ้านร้องกวาง", beneficiary.Under);
        Assert.Equal("1", beneficiary.Moo);
    }

    [Theory]
    [InlineData("Input.Mobile", "08A1234567")]
    [InlineData("Input.Mobile", "081234567")]
    [InlineData("Input.Beneficiary1.Mobile", "08123X5678")]
    [InlineData("Input.Beneficiary1.Mobile", "08123456789")]
    [InlineData("Input.Beneficiary1.PersonalIdCard", "123456789012X")]
    public async Task Member_form_rejects_malformed_contact_numbers(string field, string value)
    {
        var html = await client.GetStringAsync("/Members/Create");
        var fields = Fields();
        fields["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken");
        fields[field] = value;
        using var response = await client.PostAsync("/Members/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members.ToListAsync());
    }

    [Fact]
    public async Task History_date_filter_limits_results_before_pagination()
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var day in new[] { 1, 2, 3 }) db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Action = "member.created", EntityType = "member",
                EntityId = day.ToString(), OccurredAtUtc = new DateTimeOffset(2026, 9, day, 12, 0, 0, TimeSpan.Zero), ActorDisplayName = $"Day-{day}"
            });
            await db.SaveChangesAsync();
        }
        var html = await client.GetStringAsync("/History?from=2026-09-02&to=2026-09-02");
        Assert.Contains("Day-2", html);
        Assert.DoesNotContain("Day-1", html);
        Assert.DoesNotContain("Day-3", html);
    }

    [Fact]
    public async Task Member_search_page_counts_filtered_rows_and_fetches_only_requested_page()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var number = 1; number <= 3; number++)
        {
            var member = TestData.Member();
            member.Id = Guid.NewGuid();
            member.RunNo = number.ToString("D5");
            member.FirstName = number switch { 1 => "Z", 2 => "Y", _ => "A" };
            db.Members.Add(member);
        }
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<MemberApplicationService>();
        var page = await service.SearchPageAsync(new MemberSearchQuery(null, null, null, null, null, null, null, null, null, null), 2, 2);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("00003", Assert.Single(page.Items).RunNo);
        var lastPage = await service.SearchPageAsync(new MemberSearchQuery(null, null, null, null, null), 999, 2);
        Assert.Equal("00003", Assert.Single(lastPage.Items).RunNo);
        var sorted = await service.SearchPageAsync(new MemberSearchQuery(null, null, null, null, null), 1, 2,
            sortColumn: "name", sortDirection: "asc");
        Assert.Equal(["00003", "00002"], sorted.Items.Select(member => member.RunNo));
    }

    [Fact]
    public void Member_csv_uses_selected_headers_and_never_exports_actions()
    {
        var member = TestData.Member();
        member.FirstName = "ทดสอบ,หนึ่ง";
        var csv = ChapanakitCare.Web.Pages.Members.MemberRegistryCsv.Create(
            [member], new Dictionary<Guid, IReadOnlyList<MemberBeneficiary>>(),
            ["name", "runNo", "actions"], ["name.firstName", "name.lastName"]);
        Assert.StartsWith("\uFEFFชื่อ–นามสกุล,เลขทะเบียน\r\n", csv);
        Assert.Contains("\"ทดสอบ,หนึ่ง สมาชิก\",00001", csv);
        Assert.DoesNotContain("จัดการ", csv);
    }

    [Fact]
    public async Task Recipient_photo_survives_confirmation_and_download_with_gregorian_death_dates()
    {
        var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0ioAAAAASUVORK5CYII=");
        using var response = await ConfirmDeath(image);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var death = await db.DeathCases.SingleAsync();
        Assert.Equal(new DateOnly(2026, 9, 7), death.DeathCertificateDate);
        using var photo = await client.GetAsync($"/Deaths?handler=RecipientPhoto&id={death.Id}&slot=1");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await photo.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Fake_recipient_image_cannot_confirm_death_or_change_member_balance()
    {
        using var response = await ConfirmDeath("not an image"u8.ToArray());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.DeathCases.ToListAsync());
        var member = await db.Members.SingleAsync();
        Assert.Equal(MemberStatus.Normal, member.Status);
        Assert.Equal(30, member.AdvanceUnitsBalance);
    }

    private async Task<HttpResponseMessage> ConfirmDeath(byte[] photo)
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(TestData.Member());
            db.MemberBeneficiaries.Add(TestData.Beneficiary(1));
            await db.SaveChangesAsync();
        }
        var html = await client.GetStringAsync("/Deaths/Create?runNo=00001");
        using var form = new MultipartFormDataContent();
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["RunNo"] = "00001", ["CertificateNo"] = "DC-1", ["Cause"] = "เหตุ",
            ["CertificateDate"] = "2026-09-07", ["ReportedCertificateDate"] = "2026-09-08"
        }) form.Add(new StringContent(value), key);
        var content = new ByteArrayContent(photo);
        content.Headers.ContentType = new("image/png");
        form.Add(content, "RecipientPhoto1", "recipient.png");
        return await client.PostAsync("/Deaths/Create?handler=Confirm", form);
    }

    [Fact]
    public async Task History_navigation_reaches_events_older_than_200_without_recording_reads()
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 205; i++) db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Action = "member.created", EntityType = "member",
                EntityId = i.ToString(), OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(i), ActorDisplayName = $"Operator-{i:D3}"
            });
            await db.SaveChangesAsync();
        }
        var html = await client.GetStringAsync("/History?pageNumber=5");
        Assert.Contains("Operator-000", html);
        Assert.DoesNotContain("Operator-204", html);
        await using var checkScope = app.Services.CreateAsyncScope();
        Assert.Equal(205, await checkScope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents.CountAsync());
    }

    internal static Dictionary<string, string> Fields() => new()
    {
        ["Input.Title"] = "นาย", ["Input.FirstName"] = "สมาชิก", ["Input.LastName"] = "ทดสอบ",
        ["Input.Gender"] = "ชาย", ["Input.PersonalIdCard"] = "1234567890123",
        ["Input.BirthDate"] = "2004-07-19", ["Input.ApplicationDate"] = "2026-09-08", ["Input.ApprovalDate"] = "2026-09-08",
        ["Input.HouseNo"] = "11", ["Input.Under"] = "บ้านร้องกวาง", ["Input.Moo"] = "1",
        ["Input.Subdistrict"] = "ร้องกวาง", ["Input.District"] = "ร้องกวาง", ["Input.Province"] = "แพร่",
        ["Input.PostalCode"] = "54140", ["Input.GroupNo"] = "0101",
        ["Input.Beneficiary1.SlotNo"] = "1", ["Input.Beneficiary1.Title"] = "นาง",
        ["Input.Beneficiary1.FirstName"] = "ผู้รับ", ["Input.Beneficiary1.LastName"] = "ทดสอบ",
        ["Input.Beneficiary1.Relationship"] = "มารดา", ["Input.Beneficiary1.PersonalIdCard"] = "2222222222222",
        ["Input.Beneficiary1.Mobile"] = "0811111111", ["Input.Beneficiary1.HouseNo"] = "12",
        ["Input.Beneficiary1.Under"] = "หมู่บ้าน", ["Input.Beneficiary1.Moo"] = "2",
        ["Input.Beneficiary1.Subdistrict"] = "ร้องกวาง", ["Input.Beneficiary1.District"] = "ร้องกวาง",
        ["Input.Beneficiary1.Province"] = "แพร่", ["Input.Beneficiary1.PostalCode"] = "54140"
    };

    internal static string InputValue(string html, string name) => WebUtility.HtmlDecode(
        Regex.Match(Regex.Matches(html, "<input\\b[^>]*>").Cast<Match>()
            .Single(x => x.Value.Contains($"name=\"{name}\"", StringComparison.Ordinal)).Value, "value=\"([^\"]*)\"").Groups[1].Value);

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}
