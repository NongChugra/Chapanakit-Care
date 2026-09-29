using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Coordinators;

public sealed class IndexModel(CoordinatorApplicationService coordinatorService) : PageModel
{
    public IReadOnlyList<CoordinatorPositionView> Positions { get; private set; } = [];
    public IReadOnlyList<CoordinatorCandidateView> Candidates { get; private set; } = [];
    public IReadOnlyList<ChapanakitCare.Domain.Entities.CoordinatorEvent> History { get; private set; } = [];
    public CoordinatorPositionView? SelectedPosition { get; private set; }
    public IReadOnlyList<CoordinatorPositionView> FilteredPositions => Positions.Where(position =>
        (string.IsNullOrWhiteSpace(RoleFilter) || position.RoleCode == RoleFilter) &&
        (string.IsNullOrWhiteSpace(GroupFilter) || position.GroupNo == GroupFilter)).ToList();
    public IReadOnlyList<string> GroupOptions => Positions.Where(position => !string.IsNullOrWhiteSpace(position.GroupNo))
        .Select(position => position.GroupNo!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    [BindProperty(SupportsGet = true)] public string? PositionKey { get; set; }
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? RoleFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? GroupFilter { get; set; }
    [BindProperty] public Guid? MemberId { get; set; }
    [BindProperty] public int ExpectedVersion { get; set; }
    [BindProperty] public Guid? ExpectedMemberId { get; set; }
    [BindProperty] public string? Reason { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageAsync();
    }

    public async Task<IActionResult> OnPostChangeAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPageAsync(keepPostedConcurrency: true);
            return Page();
        }

        try
        {
            await coordinatorService.ChangeAsync(
                new CoordinatorChangeCommand(PositionKey ?? string.Empty, Action ?? string.Empty, MemberId,
                    ExpectedVersion, ExpectedMemberId, Reason),
                DateOnly.FromDateTime(DateTime.Today),
                DateTimeOffset.UtcNow,
                "ผู้ใช้ในเครื่อง",
                HttpContext.RequestAborted);
            TempData["Success"] = Action switch
            {
                "replace" => "บันทึกการเปลี่ยนผู้รับผิดชอบแล้ว",
                "end" => "บันทึกการสิ้นสุดหน้าที่แล้ว",
                _ => "บันทึกการแต่งตั้งผู้รับผิดชอบแล้ว"
            };
            return RedirectToPage();
        }
        catch (MemberValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadPageAsync(keepPostedConcurrency: true);
            return Page();
        }
    }

    private async Task LoadPageAsync(bool keepPostedConcurrency = false)
    {
        Positions = await coordinatorService.GetPositionsAsync(HttpContext.RequestAborted);
        History = await coordinatorService.GetHistoryAsync(HttpContext.RequestAborted);
        SelectedPosition = Positions.SingleOrDefault(value => value.Key == PositionKey);
        if (SelectedPosition is null)
        {
            return;
        }

        Action = NormalizeAction(Action, SelectedPosition);
        if (!keepPostedConcurrency)
        {
            ExpectedVersion = SelectedPosition.Version;
            ExpectedMemberId = SelectedPosition.MemberId;
        }
        if (Action != "end")
        {
            Candidates = await coordinatorService.SearchCandidatesAsync(SelectedPosition.Key, Search, HttpContext.RequestAborted);
        }
    }

    private static string NormalizeAction(string? action, CoordinatorPositionView position) =>
        action is "appoint" or "replace" or "end"
            ? action
            : position.MemberId is null ? "appoint" : "replace";
}
