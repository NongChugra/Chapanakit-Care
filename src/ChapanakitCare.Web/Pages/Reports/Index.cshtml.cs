using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace ChapanakitCare.Web.Pages.Reports;

public sealed class IndexModel(ReportApplicationService reports) : PageModel
{
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly From { get; set; }
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly To { get; set; }
    public string DefaultMonth => From.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public IReadOnlyList<ManagerGroupOption> ManagerGroups { get; private set; } = [];

    public async Task OnGetAsync()
    {
        if (From == default) From = await reports.GetLatestMemberApplicationMonthAsync(HttpContext.RequestAborted) ?? new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (To == default) To = new DateOnly(From.Year, From.Month, DateTime.DaysInMonth(From.Year, From.Month));
        ManagerGroups = await reports.GetManagerGroupOptionsAsync(HttpContext.RequestAborted);
    }

    public Task<IActionResult> OnGetMemberByManagerAsync(string groupNo) => DownloadByManagerAsync(groupNo);
    public async Task<IActionResult> OnGetAllMembersAsync()
    {
        var bytes = await reports.GenerateAllMembersAsync(HttpContext.RequestAborted);
        return File(bytes, "application/pdf", "all-members.pdf");
    }
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
        if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) || from.Year > 9456)
        {
            return BadRequest("กรุณาเลือกเดือนรายงาน");
        }
        From = from;
        To = from.AddMonths(1).AddDays(-1);
        return await DownloadAsync(name, generate);
    }

    private async Task<IActionResult> DownloadByManagerAsync(string groupNo)
    {
        if (string.IsNullOrWhiteSpace(groupNo)) return BadRequest("กรุณาเลือกกลุ่มสมาชิก");
        var bytes = await reports.GenerateMemberByManagerAsync(groupNo, HttpContext.RequestAborted);
        return File(bytes, "application/pdf", $"members-group-{groupNo}.pdf");
    }
}
