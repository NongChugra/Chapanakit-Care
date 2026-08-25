using System.ComponentModel.DataAnnotations;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Deaths;

public sealed class CreateModel(DeathApplicationService service) : PageModel
{
    [BindProperty, Required] public string RunNo { get; set; } = string.Empty;
    [BindProperty, Required] public string CertificateNo { get; set; } = string.Empty;
    [BindProperty, Required] public DateOnly? CertificateDate { get; set; }
    [BindProperty] public IFormFile? CertificatePdf { get; set; }
    [BindProperty, Required] public string Cause { get; set; } = string.Empty;
    [BindProperty] public bool NonPay { get; set; }
    [BindProperty] public string? NonPayReason { get; set; }
    public DeathPreview? Preview { get; private set; }
    public DeathPreview? PayablePreview { get; private set; }
    public DeathPreview? NonPayPreview { get; private set; }
    public string NextDeathCaseNo { get; private set; } = string.Empty;

    public async Task OnGetAsync(string? runNo)
    {
        RunNo = runNo ?? string.Empty;
        CertificateDate = DateOnly.FromDateTime(DateTime.Today);
        await LoadPreviewAsync();
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        ModelState.Remove(nameof(CertificateNo));
        ModelState.Remove(nameof(Cause));
        ModelState.Remove(nameof(CertificateDate));
        ModelState.Remove(nameof(CertificatePdf));
        await LoadPreviewAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        await LoadPreviewAsync();
        if (NonPay && string.IsNullOrWhiteSpace(NonPayReason))
            ModelState.AddModelError(nameof(NonPayReason), "กรุณาระบุเหตุผลเคสไม่จ่าย");
        if (CertificatePdf is null || CertificatePdf.Length == 0)
            ModelState.AddModelError(nameof(CertificatePdf), "กรุณาแนบใบมรณะบัตร PDF");
        else if (CertificatePdf.Length > 10 * 1024 * 1024)
            ModelState.AddModelError(nameof(CertificatePdf), "ไฟล์ใบมรณะบัตร PDF ต้องมีขนาดไม่เกิน 10 MB");
        if (!ModelState.IsValid) return Page();

        try
        {
            await using var input = CertificatePdf!.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer);
            var result = await service.ConfirmAsync(new(
                RunNo, CertificateNo, CertificateDate!.Value, Cause, NonPay, NonPayReason,
                new DeathCertificateDocument(CertificatePdf.FileName, CertificatePdf.ContentType, buffer.ToArray())),
                DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, "ผู้ใช้งานเครื่องนี้");
            TempData["Success"] = $"บันทึก {result.DeathCase.DeathCaseNo} ยอดสุทธิ {result.Calculation.TotalBenefitSatang / 100m:N2} บาท";
            return RedirectToPage("Index");
        }
        catch (MemberValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task LoadPreviewAsync()
    {
        Preview = null;
        PayablePreview = null;
        NonPayPreview = null;
        NextDeathCaseNo = await service.GetNextDeathCaseNoAsync();
        if (string.IsNullOrWhiteSpace(RunNo)) return;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            PayablePreview = await service.PreviewAsync(RunNo, false, today);
            NonPayPreview = await service.PreviewAsync(RunNo, true, today);
            Preview = NonPay ? NonPayPreview : PayablePreview;
        }
        catch (MemberValidationException exception) { ModelState.AddModelError(nameof(RunNo), exception.Message); }
    }
}
