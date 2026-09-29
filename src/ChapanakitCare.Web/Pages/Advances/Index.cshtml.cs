using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Advances;

public sealed class IndexModel(AppDbContext database, AdvanceResetService reset, NotificationService notifications) : PageModel
{
    public IReadOnlyList<Member> Members { get; private set; } = [];
    public IReadOnlyList<Notification> Notices { get; private set; } = [];
    public AdvanceResetBatch? Latest { get; private set; }
    public int TargetUnits { get; private set; }
    public bool AccountingActive { get; private set; }

    [BindProperty] public string ResetToken { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        await notifications.RefreshAsync(DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow);
        ResetToken = Guid.NewGuid().ToString("N");
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        if (!Guid.TryParseExact(ResetToken, "N", out _))
        {
            ModelState.AddModelError(string.Empty, "คำขอรีเซ็ตไม่ถูกต้อง กรุณาเปิดหน้ารีเซ็ตใหม่");
            ResetToken = Guid.NewGuid().ToString("N");
            await LoadAsync();
            return Page();
        }

        try
        {
        var now = DateTimeOffset.UtcNow;
        var batch = await reset.ResetAsync("manual", $"manual-{ResetToken}", DateOnly.FromDateTime(DateTime.Today), now, "ผู้ใช้งานเครื่องนี้");
        TempData["Success"] = $"รีเซ็ตยอดสมาชิกปกติเป็น {batch.TargetUnits} คนแล้ว ({batch.ResetNo})";
        return RedirectToPage();
        }
        catch (MemberValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync();
            return Page();
        }
    }

    private async Task LoadAsync()
    {
        AccountingActive = await database.AccountingBooks.AnyAsync(x => x.Code == AccountingBookCode.Welfare && x.IsActivated);
        Members = await database.Members.AsNoTracking().Where(value => value.ArchivedAtUtc == null).OrderBy(value => value.RunNo).ToListAsync();
        Notices = await database.Notifications.AsNoTracking().Where(value => value.State == "active").OrderByDescending(value => value.TriggeredBusinessDate).ToListAsync();
        Latest = (await database.AdvanceResetBatches.AsNoTracking().ToListAsync()).OrderByDescending(value => value.ConfirmedAtUtc).FirstOrDefault();
        TargetUnits = (await database.SystemSettings.AsNoTracking().SingleAsync()).ResetTargetUnits;
    }
}
