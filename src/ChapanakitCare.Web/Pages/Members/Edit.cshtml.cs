using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Members;

public sealed class EditModel(
    AppDbContext appDbContext,
    MemberApplicationService memberService) : MemberEditorPageModel(appDbContext)
{
    public string RunNo { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var member = await Database.Members.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id);
        if (member is null)
        {
            return NotFound();
        }

        var beneficiaries = await Database.MemberBeneficiaries.AsNoTracking()
            .Where(value => value.MemberId == id && value.IsActive)
            .OrderBy(value => value.SlotNo)
            .ToListAsync();
        RunNo = member.RunNo;
        Input = MemberFormInput.From(member, beneficiaries);
        await LoadSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        await LoadSettingsAsync();
        var member = await Database.Members.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id);
        if (member is null)
        {
            return NotFound();
        }

        RunNo = member.RunNo;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var command = Input.ToUpdateCommand();
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
            await memberService.UpdateAsync(id, command, DateTimeOffset.UtcNow, "ผู้ใช้งานเครื่องนี้");
            TempData["Success"] = $"แก้ไขข้อมูลสมาชิกเลขที่ {member.RunNo} เรียบร้อยแล้ว";
            return RedirectToPage("/Members/Index");
        }
        catch (Exception exception) when (exception is MemberValidationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }
}
