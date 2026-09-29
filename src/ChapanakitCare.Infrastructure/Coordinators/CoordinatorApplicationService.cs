using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Coordinators;

public static class CoordinatorRoles
{
    public const string Chairperson = "chairperson";
    public const string GroupLeader = "group_leader";
    public static string Label(string code) => code == Chairperson ? "ประธาน" : "หัวหน้ากลุ่ม";
    public static string Scope(string? groupNo) => groupNo is null ? "ทั้งสมาคม" : $"กลุ่ม {groupNo}";
}

public sealed record CoordinatorPositionView(string Key, string RoleCode, string? GroupNo, Guid? MemberId,
    string? MemberName, string? RunNo, DateOnly? AppointedOn, int Version, int MemberCount)
{
    public string RoleLabel => CoordinatorRoles.Label(RoleCode);
    public string ScopeLabel => CoordinatorRoles.Scope(GroupNo);
}

public sealed record CoordinatorRoleView(string PositionKey, string RoleCode, string? GroupNo)
{
    public string RoleLabel => CoordinatorRoles.Label(RoleCode);
    public string ScopeLabel => CoordinatorRoles.Scope(GroupNo);
}

public sealed record CoordinatorCandidateView(Guid Id, string RunNo, string Name, string? GroupNo);
public sealed record CoordinatorChangeCommand(string PositionKey, string Action, Guid? MemberId,
    int ExpectedVersion, Guid? ExpectedMemberId, string? Reason);

public sealed class CoordinatorApplicationService(AppDbContext database)
{
    public async Task<IReadOnlyList<CoordinatorPositionView>> GetPositionsAsync(CancellationToken ct = default)
    {
        var members = await database.Members.AsNoTracking().Where(x => x.ArchivedAtUtc == null)
            .Select(x => new { x.Id, x.RunNo, x.Title, x.FirstName, x.LastName, x.GroupNo, x.Status }).ToListAsync(ct);
        var positions = await database.CoordinatorPositions.AsNoTracking().ToListAsync(ct);
        var groups = members.Select(x => x.GroupNo).Concat(positions.Select(x => x.GroupNo))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var slots = new[] { (Key: CoordinatorRoles.Chairperson, Group: (string?)null) }
            .Concat(groups.Select(x => (Key: "group:" + x, Group: (string?)x)));
        return slots.Select(slot =>
        {
            var position = positions.SingleOrDefault(x => x.PositionKey == slot.Key);
            var member = members.SingleOrDefault(x => x.Id == position?.MemberId);
            return new CoordinatorPositionView(slot.Key, slot.Group is null ? CoordinatorRoles.Chairperson : CoordinatorRoles.GroupLeader,
                slot.Group, position?.MemberId, member is null ? null : Name(member.Title, member.FirstName, member.LastName),
                member?.RunNo, position?.AppointedOn, position?.Version ?? 0,
                members.Count(x => x.Status == MemberStatus.Normal && (slot.Group is null || x.GroupNo == slot.Group)));
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, CoordinatorRoleView>> GetMemberRolesAsync(CancellationToken ct = default) =>
        (await database.CoordinatorPositions.AsNoTracking().Where(x => x.MemberId != null).ToListAsync(ct))
        .ToDictionary(x => x.MemberId!.Value, x => new CoordinatorRoleView(x.PositionKey, x.RoleCode, x.GroupNo));

    public async Task<IReadOnlyList<CoordinatorCandidateView>> SearchCandidatesAsync(string positionKey, string? search, CancellationToken ct = default)
    {
        var (_, group) = ParseKey(positionKey);
        var query = database.Members.AsNoTracking().Where(x => x.Status == MemberStatus.Normal && x.ArchivedAtUtc == null &&
            !database.CoordinatorPositions.Any(p => p.MemberId == x.Id));
        if (group is not null) query = query.Where(x => x.GroupNo == group);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            query = query.Where(x => x.RunNo.Contains(text) || (x.FirstName + " " + x.LastName).Contains(text));
        }
        return (await query.OrderBy(x => x.RunNo).Take(100).ToListAsync(ct))
            .Select(x => new CoordinatorCandidateView(x.Id, x.RunNo, Name(x.Title, x.FirstName, x.LastName), x.GroupNo)).ToList();
    }

    public async Task<IReadOnlyList<CoordinatorEvent>> GetHistoryAsync(CancellationToken ct = default) =>
        await database.CoordinatorEvents.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.PositionVersion).ThenBy(x => x.Id).Take(100).ToListAsync(ct);

    public async Task ChangeAsync(CoordinatorChangeCommand command, DateOnly businessDate, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        var (role, group) = ParseKey(command.PositionKey);
        if (command.Action is not ("appoint" or "replace" or "end"))
            throw new MemberValidationException("กรุณาเลือกการดำเนินการที่ถูกต้อง");
        if (command.Action != "appoint" && string.IsNullOrWhiteSpace(command.Reason))
            throw new MemberValidationException("กรุณาระบุเหตุผลในการเปลี่ยนหรือสิ้นสุดหน้าที่");
        if (command.Reason?.Length > 1000)
            throw new MemberValidationException("เหตุผลต้องไม่เกิน 1,000 ตัวอักษร");
        if (command.ExpectedVersion < 0 || (command.Action == "end" ? command.MemberId != null : command.MemberId is null))
            throw new MemberValidationException("กรุณาเลือกสมาชิกและตรวจสอบการดำเนินการ");

        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(ct);
            var position = await database.CoordinatorPositions.SingleOrDefaultAsync(x => x.PositionKey == command.PositionKey, ct);
            if ((position?.Version ?? 0) != command.ExpectedVersion || position?.MemberId != command.ExpectedMemberId)
                throw StaleSelection();
            if (command.Action == "appoint" ? position?.MemberId != null : position?.MemberId == null)
                throw new MemberValidationException("ตำแหน่งนี้เปลี่ยนแปลงแล้ว กรุณาเลือกการแต่งตั้ง เปลี่ยนผู้รับผิดชอบ หรือสิ้นสุดหน้าที่ให้ตรงกับสถานะปัจจุบัน");

            Member? next = null;
            if (command.MemberId is not null)
            {
                next = await database.Members.SingleOrDefaultAsync(x => x.Id == command.MemberId, ct);
                if (next is null || next.Status != MemberStatus.Normal || next.ArchivedAtUtc is not null)
                    throw new MemberValidationException("เลือกได้เฉพาะสมาชิกสถานะปกติที่ยังอยู่ในทะเบียน");
                if (group is not null && next.GroupNo != group)
                    throw new MemberValidationException("หัวหน้ากลุ่มต้องเป็นสมาชิกของกลุ่มที่รับผิดชอบ");
                if (await database.CoordinatorPositions.AnyAsync(x => x.MemberId == next.Id, ct))
                    throw new MemberValidationException("สมาชิกคนนี้มีบทบาทอยู่แล้ว สมาชิกแต่ละคนมีได้เพียงหนึ่งบทบาท กรุณาสิ้นสุดหน้าที่เดิมก่อน");
            }

            if (position is null)
            {
                position = new CoordinatorPosition { PositionKey = command.PositionKey, RoleCode = role, GroupNo = group };
                database.CoordinatorPositions.Add(position);
            }
            var previous = position.MemberId is null ? null : await database.Members.SingleAsync(x => x.Id == position.MemberId, ct);
            AppendTransition(database, position, previous, next, command.Action, command.Reason?.Trim(), businessDate, now, actor);
            await database.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw StaleSelection(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { throw StaleSelection(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { throw StaleSelection(); }
    }

    private static (string Role, string? Group) ParseKey(string key)
    {
        if (key == CoordinatorRoles.Chairperson) return (CoordinatorRoles.Chairperson, null);
        if (key?.StartsWith("group:", StringComparison.Ordinal) == true && !string.IsNullOrWhiteSpace(key[6..]) && key[6..] == key[6..].Trim())
            return (CoordinatorRoles.GroupLeader, key[6..]);
        throw new MemberValidationException("ไม่พบตำแหน่งที่เลือก");
    }

    private static MemberValidationException StaleSelection() => new("ข้อมูลผู้ประสานงานเปลี่ยนแปลงแล้ว กรุณาโหลดหน้าใหม่และตรวจสอบก่อนบันทึกอีกครั้ง");
    private static string Name(string? title, string first, string last) => $"{title}{first} {last}".Trim();

    internal static async Task EndForMemberAsync(AppDbContext db, Member member, string reason, DateOnly date,
        DateTimeOffset now, string actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Ending a role with a member status change requires the existing transaction.");
        var position = await db.CoordinatorPositions.SingleOrDefaultAsync(x => x.MemberId == member.Id, ct);
        if (position is not null)
            AppendTransition(db, position, member, null, "end", reason, date, now, actor);
    }

    internal static async Task EnsureGroupChangeAllowedAsync(AppDbContext db, Member member, string? nextGroup, CancellationToken ct)
    {
        if (member.GroupNo != nextGroup && await db.CoordinatorPositions.AnyAsync(x => x.MemberId == member.Id && x.RoleCode == CoordinatorRoles.GroupLeader, ct))
            throw new MemberValidationException("สมาชิกคนนี้เป็นหัวหน้ากลุ่ม กรุณาสิ้นสุดหน้าที่ในหน้ากำหนดผู้ประสานงานก่อนย้ายกลุ่ม");
    }

    private static void AppendTransition(AppDbContext db, CoordinatorPosition position, Member? previous, Member? next,
        string action, string? reason, DateOnly date, DateTimeOffset now, string actor)
    {
        position.MemberId = next?.Id;
        position.AppointedOn = next is null ? null : date;
        position.Version++;
        var eventId = Guid.NewGuid();
        db.CoordinatorEvents.Add(new CoordinatorEvent
        {
            Id = eventId,
            PositionKey = position.PositionKey,
            RoleCode = position.RoleCode,
            GroupNo = position.GroupNo,
            Action = action,
            PreviousMemberId = previous?.Id,
            PreviousMemberName = previous is null ? null : Name(previous.Title, previous.FirstName, previous.LastName),
            PreviousRunNo = previous?.RunNo,
            MemberId = next?.Id,
            MemberName = next is null ? null : Name(next.Title, next.FirstName, next.LastName),
            MemberRunNo = next?.RunNo,
            EffectiveDate = date,
            OccurredAtUtc = now,
            Actor = actor,
            Reason = reason,
            PositionVersion = position.Version
        });
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OperationId = eventId,
            OccurredAtUtc = now,
            ActorUserId = "local-user",
            ActorDisplayName = actor,
            MachineName = Environment.MachineName,
            Action = "coordinator." + action,
            EntityType = "coordinator_position",
            EntityId = position.PositionKey,
            MemberId = next?.Id ?? previous?.Id,
            Reason = $"{(position.RoleCode == CoordinatorRoles.Chairperson ? "ประธาน" : $"หัวหน้ากลุ่ม {position.GroupNo}")} · " +
                $"{(previous is null ? "ว่าง" : $"{previous.RunNo} {Name(previous.Title, previous.FirstName, previous.LastName)}")} → " +
                $"{(next is null ? "ว่าง" : $"{next.RunNo} {Name(next.Title, next.FirstName, next.LastName)}")} · {reason}",
            AppVersion = "coordinators-v1"
        });
    }
}
