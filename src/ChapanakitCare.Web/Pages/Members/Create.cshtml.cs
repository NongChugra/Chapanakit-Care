using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace ChapanakitCare.Web.Pages.Members;

public sealed class CreateModel(
    AppDbContext database,
    MemberApplicationService memberService) : MemberEditorPageModel(database)
{
    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        Input.ApplicationDate = today;
        Input.ApprovalDate = today;
        await LoadSettingsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadSettingsAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var command = Input.ToRegisterCommand();
        foreach (var issue in InteractiveMemberRegistrationPolicy.Validate(command))
        {
            ModelState.AddModelError($"Input.{issue.Field}", issue.Message);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var member = await memberService.RegisterAsync(
                command,
                DateOnly.FromDateTime(DateTime.Today),
                DateTimeOffset.UtcNow,
                "ผู้ใช้งานเครื่องนี้");
            TempData["Success"] = $"เพิ่มสมาชิกเลขที่ {member.RunNo} เรียบร้อยแล้ว";
            return RedirectToPage("/Members/Index");
        }
        catch (MemberValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }
}
