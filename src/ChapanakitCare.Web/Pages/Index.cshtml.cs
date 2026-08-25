using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChapanakitCare.Web.Pages;

public sealed class IndexModel(
    AppDbContext database,
    SettingsApplicationService settingsService,
    DemoImportService demoImportService,
    DemoDataMaintenanceService demoDataMaintenanceService,
    BackupApplicationService backupService,
    IWebHostEnvironment environment) : PageModel
{
    [BindProperty]
    public SettingsInput Input { get; set; } = new();

    public int TotalMembers { get; private set; }
    public int NormalMembers { get; private set; }
    public int DeceasedMembers { get; private set; }
    public int Revision { get; private set; }

    public async Task OnGetAsync()
    {
        var settings = await settingsService.GetAsync();
        Input = SettingsInput.From(settings);
        Revision = settings.SettingsRevision;
        TotalMembers = await database.Members.CountAsync(value => value.ArchivedAtUtc == null);
        NormalMembers = await database.Members.CountAsync(value => value.ArchivedAtUtc == null && value.Status == Domain.Entities.MemberStatus.Normal);
        DeceasedMembers = TotalMembers - NormalMembers;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        await settingsService.SaveAsync(
            new SettingsCommand(
                Input.RegistrationFeeBaht is null ? null : checked((long)Math.Round(Input.RegistrationFeeBaht.Value * 100m)),
                checked((int)Math.Round(Input.ServiceFeePercent * 100m)),
                checked((long)Math.Round(Input.WelfarePerMemberBaht * 100m)),
                Input.ResetTargetUnits,
                Input.CoverageWaitDays,
                Input.SpecialNonPayWindowDays,
                Input.DeathWarningThreshold),
            DateTimeOffset.UtcNow,
            "ผู้ใช้งานเครื่องนี้");
        TempData["Success"] = "บันทึกค่าตั้งต้นแล้ว ค่าชุดใหม่นี้จะใช้กับรายการที่สร้างหลังจากนี้";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostImportDemoAsync()
    {
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "demo-members.json");
        if (!System.IO.File.Exists(path))
        {
            TempData["Success"] = "ไม่พบชุดข้อมูลสาธิตสังเคราะห์ กรุณาสร้าง demo-members.json ใหม่";
            return RedirectToPage();
        }

        await using var stream = System.IO.File.OpenRead(path);
        var records = await JsonSerializer.DeserializeAsync<RegisterMemberCommand[]>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
        var imported = await demoImportService.ImportAsync(
            records,
            DateOnly.FromDateTime(DateTime.Today),
            DateTimeOffset.UtcNow,
            "นำเข้าข้อมูลสาธิตสังเคราะห์");
        TempData["Success"] = imported == 0
            ? "ไม่มีรายการใหม่: ข้อมูลสาธิตที่ตรงกันมีอยู่แล้ว"
            : $"นำเข้าสมาชิกสาธิตใหม่ {imported} รายการแล้ว โดยไม่ลบสมาชิกเดิม";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearMembersAsync()
    {
        var removed = await demoDataMaintenanceService.ClearMembersAsync(
            DateTimeOffset.UtcNow,
            "ผู้ใช้งานเครื่องนี้");
        TempData["Success"] = $"ล้างสมาชิกและข้อมูลดำเนินงานที่เกี่ยวข้อง {removed} รายการแล้ว เลขสมาชิกรอบสาธิตเริ่มใหม่ที่ 00001";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBackupAsync()
    {
        var result = await backupService.CreateAsync(
            Path.Combine(environment.ContentRootPath, "Backups"),
            DateTimeOffset.UtcNow,
            "ผู้ใช้งานเครื่องนี้");
        return PhysicalFile(result.FullPath, "application/vnd.sqlite3", result.FileName);
    }
}

public sealed class SettingsInput
{
    public decimal? RegistrationFeeBaht { get; set; }
    public decimal ServiceFeePercent { get; set; }
    public decimal WelfarePerMemberBaht { get; set; }
    public int ResetTargetUnits { get; set; }
    public int CoverageWaitDays { get; set; }
    public int SpecialNonPayWindowDays { get; set; }
    public int DeathWarningThreshold { get; set; }

    public static SettingsInput From(Domain.Entities.SystemSettings settings) => new()
    {
        RegistrationFeeBaht = settings.RegistrationFeeSatang / 100m,
        ServiceFeePercent = settings.ServiceFeeBasisPoints / 100m,
        WelfarePerMemberBaht = settings.WelfarePerMemberSatang / 100m,
        ResetTargetUnits = settings.ResetTargetUnits,
        CoverageWaitDays = settings.CoverageWaitDays,
        SpecialNonPayWindowDays = settings.SpecialNonPayWindowDays,
        DeathWarningThreshold = settings.DeathWarningThreshold
    };
}
