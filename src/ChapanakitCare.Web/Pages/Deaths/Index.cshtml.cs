using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.Deaths;

public sealed class IndexModel(AppDbContext database, DeathApplicationService deathService) : PageModel
{
    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public IReadOnlyDictionary<Guid, IReadOnlyList<DeathBeneficiarySnapshot>> BeneficiariesByCase { get; private set; }
        = new Dictionary<Guid, IReadOnlyList<DeathBeneficiarySnapshot>>();

    public async Task OnGetAsync()
    {
        Rows = await (
            from death in database.DeathCases.AsNoTracking()
            join member in database.DeathMemberSnapshots on death.Id equals member.DeathCaseId
            join calculation in database.DeathCalculations on death.Id equals calculation.DeathCaseId
            orderby death.DeathSequenceNo descending
            select new Row(death, member, calculation)).ToListAsync();
        var caseIds = Rows.Select(value => value.Case.Id).ToArray();
        BeneficiariesByCase = (await database.DeathBeneficiarySnapshots.AsNoTracking()
                .Where(value => caseIds.Contains(value.DeathCaseId))
                .OrderBy(value => value.SlotNo)
                .ToListAsync())
            .GroupBy(value => value.DeathCaseId)
            .ToDictionary(value => value.Key, value => (IReadOnlyList<DeathBeneficiarySnapshot>)value.ToArray());
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

    public sealed record Row(DeathCase Case, DeathMemberSnapshot Member, DeathCalculation Calculation);
}
