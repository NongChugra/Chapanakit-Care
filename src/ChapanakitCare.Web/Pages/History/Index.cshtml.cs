using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ChapanakitCare.Web.Pages.History;

public sealed class IndexModel(AppDbContext database) : PageModel
{
    public IReadOnlyList<HistoryItem> Items { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [Validation.GregorianDate]
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    public int TotalPages { get; private set; }
    public int TotalItems { get; private set; }

    public async Task OnGetAsync()
    {
        const int pageSize = 50;
        var fromUtc = StartOfLocalDay(From ?? new DateOnly(1900, 1, 1));
        var throughUtc = StartOfLocalDay((To ?? new DateOnly(9998, 12, 30)).AddDays(1));
        var filtered = database.AuditEvents.FromSqlInterpolated($"SELECT * FROM audit_events WHERE julianday(occurred_at_utc) >= julianday({fromUtc:O}) AND julianday(occurred_at_utc) < julianday({throughUtc:O})").AsNoTracking();
        TotalItems = await filtered.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalItems / (double)pageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        // DateTimeOffset is stored as ISO text in this SQLite schema; order the UTC
        // timestamp using a database expression, then fetch only this page.
        var events = await database.AuditEvents.FromSqlInterpolated($"SELECT * FROM audit_events WHERE julianday(occurred_at_utc) >= julianday({fromUtc:O}) AND julianday(occurred_at_utc) < julianday({throughUtc:O}) ORDER BY julianday(occurred_at_utc) DESC, id DESC LIMIT {pageSize} OFFSET {(PageNumber - 1) * pageSize}")
            .AsNoTracking().ToListAsync();
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

    private static DateTimeOffset StartOfLocalDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
    }
}

public sealed record HistoryItem(AuditEvent Audit, Member? Member, IReadOnlyList<AuditFieldChange> Changes);
