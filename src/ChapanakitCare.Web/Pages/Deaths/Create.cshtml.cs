using System.ComponentModel.DataAnnotations;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Deaths;

[RequestSizeLimit(32 * 1024 * 1024)]
public sealed class CreateModel(DeathApplicationService service) : PageModel
{
    [BindProperty, Required(ErrorMessage = "กรุณากรอกเลขทะเบียนสมาชิก")] public string RunNo { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "กรุณากรอกเลขที่ใบมรณะบัตร")] public string CertificateNo { get; set; } = string.Empty;
    [Validation.GregorianDate]
    [BindProperty, Required(ErrorMessage = "กรุณากรอกวันที่เสียชีวิตตามใบมรณะบัตร")] public DateOnly? CertificateDate { get; set; }
    [Validation.GregorianDate]
    [BindProperty, Required(ErrorMessage = "กรุณากรอกวันที่แจ้งตามใบมรณะบัตร")] public DateOnly? ReportedCertificateDate { get; set; }
    [BindProperty] public IFormFile? CertificatePdf { get; set; }
    [BindProperty] public IFormFile? RecipientPhoto1 { get; set; }
    [BindProperty] public IFormFile? RecipientPhoto2 { get; set; }
    [BindProperty, Required(ErrorMessage = "กรุณากรอกสาเหตุการเสียชีวิต")] public string Cause { get; set; } = string.Empty;
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
        ReportedCertificateDate = DateOnly.FromDateTime(DateTime.Today);
        await LoadPreviewAsync();
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        CertificateDate ??= DateOnly.FromDateTime(DateTime.Today);
        ReportedCertificateDate ??= DateOnly.FromDateTime(DateTime.Today);
        ModelState.Remove(nameof(CertificateNo));
        ModelState.Remove(nameof(Cause));
        ModelState.Remove(nameof(CertificateDate));
        ModelState.Remove(nameof(ReportedCertificateDate));
        ModelState.Remove(nameof(CertificatePdf));
        await LoadPreviewAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        await LoadPreviewAsync();
        if (NonPay && string.IsNullOrWhiteSpace(NonPayReason))
            ModelState.AddModelError(nameof(NonPayReason), "กรุณาระบุเหตุผลเคสไม่จ่าย");
        if (CertificatePdf is not null && CertificatePdf.Length > 10 * 1024 * 1024)
            ModelState.AddModelError(nameof(CertificatePdf), "ไฟล์ใบมรณะบัตร PDF ต้องมีขนาดไม่เกิน 10 MB");
        foreach (var file in new[] { RecipientPhoto1, RecipientPhoto2 })
            if (file is not null && file.Length > RecipientPhotoDocument.MaximumBytes)
                ModelState.AddModelError(string.Empty, "ภาพผู้รับเงินต้องมีขนาดไม่เกิน 10 MB ต่อภาพ");
        if (!ModelState.IsValid) return Page();

        try
        {
            byte[] bytes;
            string fileName;
            string contentType;
            if (CertificatePdf is null || CertificatePdf.Length == 0)
            {
                bytes = "%PDF-1.4\n% ไม่มีไฟล์ใบมรณะบัตรแนบ\n%%EOF"u8.ToArray();
                fileName = "not-provided.pdf";
                contentType = "application/pdf";
            }
            else
            {
                await using var input = CertificatePdf.OpenReadStream();
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer);
                bytes = buffer.ToArray();
                fileName = CertificatePdf.FileName;
                contentType = CertificatePdf.ContentType;
            }
            var photos = new List<RecipientPhotoDocument>();
            var uploads = new[] { RecipientPhoto1, RecipientPhoto2 };
            for (var index = 0; index < uploads.Length; index++)
            {
                if (uploads[index] is not { } upload) continue;
                await using var photoStream = upload.OpenReadStream();
                using var photoBuffer = new MemoryStream();
                await photoStream.CopyToAsync(photoBuffer);
                photos.Add(new(index + 1, upload.FileName, upload.ContentType, photoBuffer.ToArray()));
            }
            var result = await service.ConfirmAsync(new(
                RunNo, CertificateNo, CertificateDate!.Value, Cause, NonPay, NonPayReason,
                new DeathCertificateDocument(fileName, contentType, bytes), photos, ReportedCertificateDate),
                DateOnly.FromDateTime(DateTime.Today), DateTimeOffset.UtcNow, "ผู้ใช้งานเครื่องนี้");
            TempData["Success"] = $"บันทึก {result.DeathCase.DeathCaseNo} ยอดสุทธิ {result.Calculation.TotalBenefitSatang / 100m:N2} บาท";
            return RedirectToPage("Index");
        }
        catch (Exception exception) when (exception is MemberValidationException or AccountingValidationException or AccountingPeriodClosedException or AccountingIdempotencyConflictException)
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
            PayablePreview = await service.PreviewAsync(RunNo, false, today, deathCertificateDate: CertificateDate);
            NonPayPreview = await service.PreviewAsync(RunNo, true, today, deathCertificateDate: CertificateDate);
            Preview = NonPay ? NonPayPreview : PayablePreview;
        }
        catch (Exception exception) when (exception is MemberValidationException or AccountingValidationException or AccountingPeriodClosedException or AccountingIdempotencyConflictException)
        { ModelState.AddModelError(nameof(RunNo), exception.Message); }
    }
}
