using System.Globalization;
using System.Diagnostics;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var localUrl = builder.Configuration["LocalUrl"] ?? "http://127.0.0.1:5188";
builder.WebHost.UseUrls(localUrl);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appData);
var databasePath = Path.Combine(appData, "chapanakit-care-demo.db");

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<MemberApplicationService>();
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
    await database.SaveChangesAsync();
    await scope.ServiceProvider.GetRequiredService<NotificationService>().RefreshAsync(DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow);
}

await app.StartAsync();
if (!string.Equals(Environment.GetEnvironmentVariable("CHAPANAKIT_NO_BROWSER"), "1", StringComparison.Ordinal))
{
    Process.Start(new ProcessStartInfo(localUrl) { UseShellExecute = true });
}

await app.WaitForShutdownAsync();
