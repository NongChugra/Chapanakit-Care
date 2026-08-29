using System.Globalization;
using System.Diagnostics;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using ChapanakitCare.Web.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var localUrl = builder.Configuration["LocalUrl"] ?? "http://127.0.0.1:5188";
builder.WebHost.UseUrls(localUrl);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appData);
var databasePath = Path.Combine(appData, "chapanakit-care-demo.db");

builder.Services.AddRazorPages().AddMvcOptions(options => ThaiModelBindingMessages.Configure(options.ModelBindingMessageProvider));
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<MemberApplicationService>();
builder.Services.AddScoped<ResignationApplicationService>();
builder.Services.AddScoped<SettingsApplicationService>();
builder.Services.AddScoped<TablePreferenceService>();
builder.Services.AddScoped<DemoImportService>();
builder.Services.AddScoped<DemoDataMaintenanceService>();
builder.Services.AddScoped<DeathApplicationService>();
builder.Services.AddScoped<AdvanceResetService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ReportApplicationService>();
builder.Services.AddScoped<BackupApplicationService>();

var app = builder.Build();
app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

var thaiCulture = CultureInfo.GetCultureInfo("th-TH");
CultureInfo.DefaultThreadCurrentCulture = thaiCulture;
CultureInfo.DefaultThreadCurrentUICulture = thaiCulture;

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();
    if (!await database.NumberSequences.AnyAsync(value => value.SequenceKey == "member_run_no"))
    {
        database.NumberSequences.Add(new NumberSequence
        {
            SequenceKey = "member_run_no",
            NextValue = 1,
            Width = 5,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }
    if (!await database.NumberSequences.AnyAsync(value => value.SequenceKey == "death_case_no")) database.NumberSequences.Add(new NumberSequence { SequenceKey = "death_case_no", Prefix = "D", NextValue = 1, Width = 5, UpdatedAtUtc = DateTimeOffset.UtcNow });
    if (!await database.NumberSequences.AnyAsync(value => value.SequenceKey == "reset_no")) database.NumberSequences.Add(new NumberSequence { SequenceKey = "reset_no", Prefix = "R", NextValue = 1, Width = 5, UpdatedAtUtc = DateTimeOffset.UtcNow });
    // Upgrade only the untouched first-day defaults; never overwrite an operator's own setting.
    var settings = await database.SystemSettings.SingleAsync();
    if (settings.ServiceFeeRoundingMode is "round_up_to_satang" or "round_down_to_satang")
    {
        if (settings.WelfarePerMemberSatang == 1_500)
        {
            settings.WelfarePerMemberSatang = 900;
        }
        settings.ServiceFeeRoundingMode = "round_down_to_baht";
        settings.SettingsRevision++;
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;
        settings.UpdatedBy = "system_default_upgrade";
    }
    var membersWithBuddhistYears = await database.Members.ToListAsync();
    foreach (var member in membersWithBuddhistYears)
    {
        member.BirthDate = member.BirthDate is null ? null : ChapanakitCare.Domain.ThaiBuddhistDate.NormalizeStoredDate(member.BirthDate.Value);
        member.ApplicationDate = ChapanakitCare.Domain.ThaiBuddhistDate.NormalizeStoredDate(member.ApplicationDate);
        member.ApprovalDate = ChapanakitCare.Domain.ThaiBuddhistDate.NormalizeStoredDate(member.ApprovalDate);
        member.CoverageStartDate = ChapanakitCare.Domain.ThaiBuddhistDate.NormalizeStoredDate(member.CoverageStartDate);
    }
    await database.SaveChangesAsync();
    await scope.ServiceProvider.GetRequiredService<NotificationService>().RefreshAsync(DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow);
}

await app.StartAsync();
if (!string.Equals(Environment.GetEnvironmentVariable("CHAPANAKIT_NO_BROWSER"), "1", StringComparison.Ordinal))
{
    // Prefer an isolated Edge app window. It avoids inheriting a crashing Chrome
    // profile and gives the local application a desktop-like taskbar window.
    var edgePath = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
    }.FirstOrDefault(File.Exists);
    if (edgePath is not null)
    {
        var edge = new ProcessStartInfo(edgePath) { UseShellExecute = false };
        edge.ArgumentList.Add($"--app={localUrl}");
        edge.ArgumentList.Add("--start-maximized");
        edge.ArgumentList.Add("--disable-crash-reporter");
        var profilePath = Path.Combine(Path.GetTempPath(), "ChapanakitCare-EdgeProfile");
        Directory.CreateDirectory(profilePath);
        edge.ArgumentList.Add($"--user-data-dir={profilePath}");
        Process.Start(edge);
    }
    else
    {
        Process.Start(new ProcessStartInfo(localUrl) { UseShellExecute = true });
    }
}

await app.WaitForShutdownAsync();
