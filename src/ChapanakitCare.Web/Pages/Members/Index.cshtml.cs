using System.Text.Json;
using System.Text;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;

namespace ChapanakitCare.Web.Pages.Members;

public sealed class IndexModel(
    MemberApplicationService memberService,
    CoordinatorApplicationService coordinatorService,
    TablePreferenceService preferenceService,
    AppDbContext database) : PageModel
{
    public IReadOnlyList<Member> Members { get; private set; } = [];
    public int TotalMembers { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalMembers / (double)PageSize));
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 25;
    public IReadOnlyDictionary<Guid, IReadOnlyList<MemberBeneficiary>> BeneficiariesByMember { get; private set; }
        = new Dictionary<Guid, IReadOnlyList<MemberBeneficiary>>();
    public IReadOnlyDictionary<Guid, CoordinatorRoleView> CoordinatorRolesByMember { get; private set; }
        = new Dictionary<Guid, CoordinatorRoleView>();
    public TablePreference Preference { get; private set; } = new([], [], MemberTableComponents.All);
    public string FilterSummary => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(Search) ? null : $"ค้นหา: {Search}",
        Status is null ? null : $"สถานะ: {Status switch { MemberStatus.Normal => "ปกติ", MemberStatus.Deceased => "เสียชีวิต", _ => "ลาออก" }}",
        string.IsNullOrWhiteSpace(Subdistrict) ? null : $"ตำบล: {Subdistrict}",
        string.IsNullOrWhiteSpace(GroupNo) ? null : $"กลุ่มสมาชิก: {GroupNo}",
        string.IsNullOrWhiteSpace(RoleFilter) ? null : $"ตำแหน่ง: {RoleFilter switch { CoordinatorRoles.Chairperson => "ประธาน", CoordinatorRoles.GroupLeader => "หัวหน้ากลุ่ม", "none" => "ไม่มีตำแหน่ง", _ => "ทั้งหมด" }}",
        string.IsNullOrWhiteSpace(Moo) ? null : $"หมู่ที่: {Moo}",
        From is null ? null : $"ตั้งแต่: {ThaiDate(From.Value)}",
        To is null ? null : $"ถึง: {ThaiDate(To.Value)}"
    }.Where(value => value is not null)) is { Length: > 0 } summary ? summary : "ทั้งหมด";

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public MemberDateField? DateField { get; set; }
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public MemberStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Subdistrict { get; set; }
    [BindProperty(SupportsGet = true)] public string? District { get; set; }
    [BindProperty(SupportsGet = true)] public string? Moo { get; set; }
    [BindProperty(SupportsGet = true)] public string? GroupNo { get; set; }
    [BindProperty(SupportsGet = true)] public string? RoleFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? Under { get; set; }

    public async Task OnGetAsync()
    {
        PageSize = PageSize is 25 or 50 or 100 ? PageSize : 25;
        PageNumber = Math.Max(1, PageNumber);
        Preference = await preferenceService.GetAsync("local-user", "member-library");
        CoordinatorRolesByMember = await coordinatorService.GetMemberRolesAsync();
        var allowed = RoleFilter is CoordinatorRoles.Chairperson or CoordinatorRoles.GroupLeader
            ? CoordinatorRolesByMember.Where(pair => pair.Value.RoleCode == RoleFilter).Select(pair => pair.Key).ToArray() : null;
        var excluded = RoleFilter == "none" ? CoordinatorRolesByMember.Keys.ToArray() : null;
        var page = await memberService.SearchPageAsync(SearchQuery(), PageNumber, PageSize, allowed, excluded,
            Preference.SortColumn, Preference.SortDirection);
        TotalMembers = page.TotalCount;
        PageNumber = Math.Min(PageNumber, TotalPages);
        Members = page.Items;
        await LoadBeneficiariesAsync();
    }

    public async Task<IActionResult> OnGetExportAsync(string[] columns, string[] components)
    {
        var members = await memberService.SearchAsync(SearchQuery());
        CoordinatorRolesByMember = await coordinatorService.GetMemberRolesAsync();
        members = RoleFilter switch
        {
            CoordinatorRoles.Chairperson or CoordinatorRoles.GroupLeader => members.Where(member =>
                CoordinatorRolesByMember.TryGetValue(member.Id, out var role) && role.RoleCode == RoleFilter).ToList(),
            "none" => members.Where(member => !CoordinatorRolesByMember.ContainsKey(member.Id)).ToList(),
            _ => members
        };
        Members = members;
        await LoadBeneficiariesAsync();
        var csv = MemberRegistryCsv.Create(members, BeneficiariesByMember, columns, components, CoordinatorRolesByMember);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "member-registry.csv");
    }

    private MemberSearchQuery SearchQuery() => new(Search, DateField, From, To, Status, Subdistrict, District, Moo, GroupNo, Under);

    public string PageUrl(int page)
    {
        var values = Request.Query.ToDictionary(pair => pair.Key, pair => (string?)pair.Value.ToString());
        values["PageNumber"] = page.ToString();
        values["PageSize"] = PageSize.ToString();
        return QueryHelpers.AddQueryString(Request.Path.ToString(), values);
    }

    private async Task LoadBeneficiariesAsync()
    {
        var memberIds = Members.Select(value => value.Id).ToArray();
        BeneficiariesByMember = (await database.MemberBeneficiaries.AsNoTracking()
                .Where(value => memberIds.Contains(value.MemberId) && value.IsActive)
                .OrderBy(value => value.SlotNo)
                .ToListAsync())
            .GroupBy(value => value.MemberId)
            .ToDictionary(value => value.Key, value => (IReadOnlyList<MemberBeneficiary>)value.ToArray());
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

    public static string ThaiDate(DateOnly date) => Domain.ThaiBuddhistDate.Format(date);

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
