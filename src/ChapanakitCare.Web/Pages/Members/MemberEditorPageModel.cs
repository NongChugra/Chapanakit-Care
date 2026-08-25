using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Members;

public abstract class MemberEditorPageModel(AppDbContext database) : PageModel
{
    protected AppDbContext Database { get; } = database;
    [BindProperty]
    public MemberFormInput Input { get; set; } = new();

    public int CoverageWaitDays { get; private set; } = 180;

    protected async Task LoadSettingsAsync() =>
        CoverageWaitDays = (await Database.SystemSettings.AsNoTracking().SingleAsync()).CoverageWaitDays;
}
