using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChapanakitCare.Web.Pages.Reports;

public sealed class IndexModel(ReportApplicationService reports) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly To { get; set; }

    public void OnGet()
    {
        if (From == default) From = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (To == default) To = From.AddMonths(1).AddDays(-1);
    }

    public Task<IActionResult> OnGetMonthlyAsync() => DownloadAsync("monthly-members", reports.GenerateMonthlySummaryAsync);
    public Task<IActionResult> OnGetDeathsAsync() => DownloadAsync("death-report", reports.GenerateDeathReportAsync);
    public Task<IActionResult> OnGetSakOneAsync() => DownloadAsync("sak-one", reports.GenerateSakOneAsync);

    private async Task<IActionResult> DownloadAsync(string name, Func<ReportPeriod, CancellationToken, Task<byte[]>> generate)
    {
        OnGet();
        var bytes = await generate(new ReportPeriod(From, To), HttpContext.RequestAborted);
        return File(bytes, "application/pdf", $"{name}-{From:yyyyMMdd}-{To:yyyyMMdd}.pdf");
    }
}
