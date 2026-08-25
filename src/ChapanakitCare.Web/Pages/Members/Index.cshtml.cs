using System.Text.Json;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Members;

public sealed class IndexModel(
    MemberApplicationService memberService,
    TablePreferenceService preferenceService,
    AppDbContext database) : PageModel
{
    public IReadOnlyList<Member> Members { get; private set; } = [];
    public IReadOnlyDictionary<Guid, IReadOnlyList<MemberBeneficiary>> BeneficiariesByMember { get; private set; }
        = new Dictionary<Guid, IReadOnlyList<MemberBeneficiary>>();
    public TablePreference Preference { get; private set; } = new([], [], MemberTableComponents.All);

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public MemberDateField? DateField { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public MemberStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Subdistrict { get; set; }
    [BindProperty(SupportsGet = true)] public string? District { get; set; }
    [BindProperty(SupportsGet = true)] public string? Moo { get; set; }
    [BindProperty(SupportsGet = true)] public string? GroupNo { get; set; }
    [BindProperty(SupportsGet = true)] public string? Under { get; set; }

    public async Task OnGetAsync()
    {
        Members = await memberService.SearchAsync(new MemberSearchQuery(
            Search, DateField, From, To, Status, Subdistrict, District, Moo, GroupNo, Under));
        var memberIds = Members.Select(value => value.Id).ToArray();
        BeneficiariesByMember = (await database.MemberBeneficiaries.AsNoTracking()
                .Where(value => memberIds.Contains(value.MemberId) && value.IsActive)
                .OrderBy(value => value.SlotNo)
                .ToListAsync())
            .GroupBy(value => value.MemberId)
            .ToDictionary(value => value.Key, value => (IReadOnlyList<MemberBeneficiary>)value.ToArray());
        Preference = await preferenceService.GetAsync("local-user", "member-library");
    }

    public async Task<IActionResult> OnPostPreferenceAsync([FromBody] PreferenceRequest request)
    {
        await preferenceService.SaveAsync(
            "local-user",
            "member-library",
            request.ColumnOrder,
            request.HiddenColumns,
            request.VisibleComponents,
            request.SortColumn,
            request.SortDirection,
            DateTimeOffset.UtcNow);
        return new JsonResult(new { saved = true });
    }

    public async Task<IActionResult> OnPostResetPreferenceAsync()
    {
        await preferenceService.ResetAsync("local-user", "member-library");
        return new JsonResult(new { reset = true });
    }

    public static string ThaiDate(DateOnly date) => $"{date:dd/MM}/{date.Year + 543}";

    public static int? Age(Member member)
    {
        if (member.BirthDate is null)
        {
            return null;
        }

        return Domain.AgeCalculator.CompletedYears(member.BirthDate.Value, DateOnly.FromDateTime(DateTime.Today));
    }
}

public sealed record PreferenceRequest(
    string[] ColumnOrder,
    string[] HiddenColumns,
    string[] VisibleComponents,
    string? SortColumn,
    string? SortDirection);
