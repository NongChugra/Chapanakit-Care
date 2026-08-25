using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Reports;

public sealed class IndexModel(ReportApplicationService reports) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly To { get; set; }
    public string DefaultMonth => $"{From:yyyy-MM}";
    public IReadOnlyList<string> ManagerGroups { get; private set; } = [];

    public async Task OnGetAsync()
    {
        if (From == default) From = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (To == default) To = From.AddMonths(1).AddDays(-1);
        ManagerGroups = await reports.GetManagerGroupsAsync(HttpContext.RequestAborted);
    }

    public Task<IActionResult> OnGetMemberByManagerAsync(string groupNo) => DownloadByManagerAsync(groupNo);
    public Task<IActionResult> OnGetMonthlyAsync(string month) => DownloadMonthAsync("monthly-members", month, reports.GenerateMonthlySummaryAsync);
    public Task<IActionResult> OnGetSakOneAsync(string month) => DownloadMonthAsync("sak-one", month, reports.GenerateSakOneAsync);

    private async Task<IActionResult> DownloadAsync(string name, Func<ReportPeriod, CancellationToken, Task<byte[]>> generate)
    {
        if (From == default) From = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (To == default) To = From.AddMonths(1).AddDays(-1);
        var bytes = await generate(new ReportPeriod(From, To), HttpContext.RequestAborted);
        return File(bytes, "application/pdf", $"{name}-{From:yyyyMMdd}-{To:yyyyMMdd}.pdf");
    }

    private async Task<IActionResult> DownloadMonthAsync(string name, string month, Func<ReportPeriod, CancellationToken, Task<byte[]>> generate)
    {
        if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", out var from))
        {
            return BadRequest("กรุณาเลือกเดือนรายงาน");
        }
        From = from;
        To = from.AddMonths(1).AddDays(-1);
        return await DownloadAsync(name, generate);
    }

    private async Task<IActionResult> DownloadByManagerAsync(string groupNo)
    {
        var bytes = await reports.GenerateMemberByManagerAsync(groupNo, HttpContext.RequestAborted);
        return File(bytes, "application/pdf", $"members-group-{groupNo}.pdf");
    }
}
