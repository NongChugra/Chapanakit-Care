using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Web.Pages.History;

public sealed class IndexModel(AppDbContext database) : PageModel
{
    public IReadOnlyList<HistoryItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var events = (await database.AuditEvents.AsNoTracking().ToListAsync())
            .OrderByDescending(value => value.OccurredAtUtc)
            .Take(200)
            .ToList();
        var eventIds = events.Select(value => value.Id).ToArray();
        var changes = await database.AuditFieldChanges.AsNoTracking()
            .Where(value => eventIds.Contains(value.AuditEventId))
            .OrderBy(value => value.FieldName)
            .ToListAsync();
        var memberIds = events.Where(value => value.MemberId != null).Select(value => value.MemberId!.Value).Distinct().ToArray();
        var members = await database.Members.AsNoTracking()
            .Where(value => memberIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id);
        Items = events.Select(audit => new HistoryItem(
            audit,
            audit.MemberId is not null && members.TryGetValue(audit.MemberId.Value, out var member) ? member : null,
            changes.Where(value => value.AuditEventId == audit.Id).ToArray())).ToArray();
    }

    public static string ActionLabel(string action) => AuditActionLabels.ToThai(action);
}

public sealed record HistoryItem(AuditEvent Audit, Member? Member, IReadOnlyList<AuditFieldChange> Changes);
