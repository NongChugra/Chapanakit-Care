using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Deaths;

public sealed class IndexModel(
    AppDbContext database,
    DeathApplicationService deathService,
    TablePreferenceService preferenceService) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Decision { get; set; }
    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public IReadOnlyDictionary<Guid, IReadOnlyList<DeathBeneficiarySnapshot>> BeneficiariesByCase { get; private set; }
        = new Dictionary<Guid, IReadOnlyList<DeathBeneficiarySnapshot>>();
    public TablePreference Preference { get; private set; } = new([], [], []);
    public string FilterSummary => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(Search) ? null : $"ค้นหา: {Search}",
        From is null ? null : $"ตั้งแต่: {ThaiDate(From.Value)}",
        To is null ? null : $"ถึง: {ThaiDate(To.Value)}",
        Decision is "payable" ? "ผลการพิจารณา: จ่าย" : Decision is "nonpay" ? "ผลการพิจารณา: ไม่จ่าย" : null
    }.Where(value => value is not null)) is { Length: > 0 } summary ? summary : "ทั้งหมด";

    public async Task OnGetAsync()
    {
        var query = (
            from death in database.DeathCases.AsNoTracking()
            join member in database.DeathMemberSnapshots on death.Id equals member.DeathCaseId
            join calculation in database.DeathCalculations on death.Id equals calculation.DeathCaseId
            orderby death.DeathSequenceNo descending
            select new Row(death, member, calculation));
        if (From is not null) query = query.Where(value => value.Case.RecordedBusinessDate >= From);
        if (To is not null) query = query.Where(value => value.Case.RecordedBusinessDate <= To);
        Rows = await query.ToListAsync();
        var text = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
            Rows = Rows.Where(value => ($"{value.Case.DeathCaseNo} {value.Member.RunNo} {value.Member.Title}{value.Member.FirstName} {value.Member.LastName} {value.Member.PersonalIdCard} {value.Member.GroupNo} {value.Case.DeathCertificateNo} {value.Case.CauseOfDeathText}").Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (Decision is "payable" or "nonpay")
            Rows = Rows.Where(value => Decision == "payable" ? value.Case.EligibilityResult == "payable" : value.Case.EligibilityResult != "payable").ToArray();
        var caseIds = Rows.Select(value => value.Case.Id).ToArray();
        BeneficiariesByCase = (await database.DeathBeneficiarySnapshots.AsNoTracking()
                .Where(value => caseIds.Contains(value.DeathCaseId))
                .OrderBy(value => value.SlotNo)
                .ToListAsync())
            .GroupBy(value => value.DeathCaseId)
            .ToDictionary(value => value.Key, value => (IReadOnlyList<DeathBeneficiarySnapshot>)value.ToArray());
        var photos = await database.DeathRecipientPhotos.AsNoTracking()
            .Where(x => caseIds.Contains(x.DeathCaseId))
            .Select(x => new { x.DeathCaseId, x.BeneficiarySlotNo }).ToListAsync();
        PhotoSlotsByCase = photos.GroupBy(x => x.DeathCaseId)
            .ToDictionary(x => x.Key, x => x.Select(p => p.BeneficiarySlotNo).Order().ToArray());
        Preference = await preferenceService.GetAsync("local-user", "death-library");
    }

    public async Task<IActionResult> OnPostPreferenceAsync([FromBody] DeathPreferenceRequest request)
    {
        await preferenceService.SaveAsync(
            "local-user", "death-library", request.ColumnOrder, request.HiddenColumns, [],
            request.SortColumn, request.SortDirection, DateTimeOffset.UtcNow);
        return new JsonResult(new { saved = true });
    }

    public async Task<IActionResult> OnPostResetPreferenceAsync()
    {
        await preferenceService.ResetAsync("local-user", "death-library");
        return new JsonResult(new { reset = true });
    }

    public async Task<IActionResult> OnGetCertificateAsync(Guid id)
    {
        try
        {
            var document = await deathService.GetCertificateAsync(id);
            return File(document.Bytes, "application/pdf", document.FileName);
        }
        catch (MemberValidationException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnGetRecipientPhotoAsync(Guid id, int slot)
    {
        var photo = await database.DeathRecipientPhotos.AsNoTracking()
            .SingleOrDefaultAsync(x => x.DeathCaseId == id && x.BeneficiarySlotNo == slot);
        if (photo is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.Bytes, photo.ContentType, photo.FileName);
    }

    public Dictionary<Guid, int[]> PhotoSlotsByCase { get; private set; } = [];

    public sealed record Row(DeathCase Case, DeathMemberSnapshot Member, DeathCalculation Calculation);
    private static string ThaiDate(DateOnly value) => ChapanakitCare.Domain.ThaiBuddhistDate.Format(value);
}

public sealed record DeathPreferenceRequest(
    string[] ColumnOrder,
    string[] HiddenColumns,
    string? SortColumn,
    string? SortDirection);
