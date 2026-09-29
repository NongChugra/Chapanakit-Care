using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Members;

public sealed record ResignationPreview(Member Member, long RefundSatang, bool AccountingActive = false);
public sealed record ResignationResult(Member Member, long RefundSatang, bool AccountingActive = false);

public sealed class ResignationApplicationService(AppDbContext database)
{
    public async Task<ResignationPreview?> PreviewAsync(string runNo, CancellationToken cancellationToken = default)
    {
        var member = await database.Members.AsNoTracking().SingleOrDefaultAsync(value => value.RunNo == runNo.Trim(), cancellationToken);
        if (member is null) return null;
        var welfare = await database.SystemSettings.AsNoTracking().Select(value => value.WelfarePerMemberSatang).SingleAsync(cancellationToken);
        var monetaryBalance = await new DeathAccountingService(database).GetAdvanceAsync(member.Id, cancellationToken);
        return new ResignationPreview(member, monetaryBalance ?? checked(member.AdvanceUnitsBalance * welfare), monetaryBalance.HasValue);
    }

    public async Task<ResignationResult> ConfirmAsync(string runNo, DateOnly businessDate, DateTimeOffset now, string actor, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var member = await database.Members.SingleOrDefaultAsync(value => value.RunNo == runNo.Trim(), cancellationToken)
            ?? throw new MemberValidationException("ไม่พบเลขทะเบียนสมาชิกนี้");
        if (member.Status != MemberStatus.Normal)
            throw new MemberValidationException("สมาชิกคนนี้ไม่ได้อยู่ในสถานะปกติ จึงไม่สามารถลาออกซ้ำได้");

        var welfare = await database.SystemSettings.AsNoTracking().Select(value => value.WelfarePerMemberSatang).SingleAsync(cancellationToken);
        var monetaryBalance = await new DeathAccountingService(database).GetAdvanceAsync(member.Id, cancellationToken);
        var refundSatang = monetaryBalance ?? checked(member.AdvanceUnitsBalance * welfare);
        var balanceBefore = member.AdvanceUnitsBalance;
        var nextOrder = (await database.AdvanceLedgerEntries.Where(value => value.MemberId == member.Id).MaxAsync(value => (long?)value.EntryOrder, cancellationToken) ?? 0) + 1;

        await CoordinatorApplicationService.EndForMemberAsync(database, member, "สิ้นสุดหน้าที่เนื่องจากสมาชิกลาออก", businessDate, now, actor, cancellationToken);
        member.Status = MemberStatus.Resigned;
        member.AdvanceUnitsBalance = 0;
        member.Version++;
        member.UpdatedAtUtc = now;
        member.UpdatedBy = actor;
        database.MemberStatusEvents.Add(new MemberStatusEvent
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            FromStatus = "normal",
            ToStatus = "resigned",
            EffectiveDate = businessDate,
            SourceType = "resignation",
            CreatedAtUtc = now,
            CreatedBy = actor
        });
        database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            EntryOrder = nextOrder,
            EntryType = monetaryBalance.HasValue ? "correction" : "resignation_refund",
            BusinessDate = businessDate,
            UnitsDelta = -balanceBefore,
            BalanceBefore = balanceBefore,
            BalanceAfter = 0,
            Reason = monetaryBalance.HasValue ? "สมาชิกลาออก ปิดตัวนับสิทธิ เงินคงเหลือยังไม่ได้จ่ายคืน" : $"สมาชิกลาออก คืนเงินสงเคราะห์ล่วงหน้า {balanceBefore} คน",
            CreatedAtUtc = now,
            CreatedBy = actor
        });
        database.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            OccurredAtUtc = now,
            ActorUserId = "local-user",
            ActorDisplayName = actor,
            MachineName = Environment.MachineName,
            Action = "member.resigned",
            EntityType = "member",
            EntityId = member.Id.ToString(),
            MemberId = member.Id,
            AppVersion = "checkpoint-6"
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ResignationResult(member, refundSatang, monetaryBalance.HasValue);
    }
}
