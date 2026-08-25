using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Resignations;

public sealed class IndexModel(ResignationApplicationService resignationService) : PageModel
{
    [BindProperty] public string RunNo { get; set; } = string.Empty;
    public ResignationPreview? Preview { get; private set; }

    public async Task<IActionResult> OnPostSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(RunNo))
        {
            ModelState.AddModelError(nameof(RunNo), "กรุณากรอกเลขทะเบียนสมาชิก");
            return Page();
        }
        Preview = await resignationService.PreviewAsync(RunNo, HttpContext.RequestAborted);
        if (Preview is null) ModelState.AddModelError(nameof(RunNo), "ไม่พบเลขทะเบียนสมาชิกนี้");
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        try
        {
            var result = await resignationService.ConfirmAsync(RunNo, DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, "ผู้ใช้งานเครื่องนี้", HttpContext.RequestAborted);
            TempData["Success"] = $"ปรับสถานะสมาชิกเลขที่ {result.Member.RunNo} เป็นลาออกแล้ว และคืนเงิน {result.RefundSatang / 100m:N2} บาท";
            return RedirectToPage();
        }
        catch (MemberValidationException exception)
        {
            ModelState.AddModelError(nameof(RunNo), exception.Message);
            Preview = await resignationService.PreviewAsync(RunNo, HttpContext.RequestAborted);
            return Page();
        }
    }
}
