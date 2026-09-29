using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateCoordinatorWritesAsync(CancellationToken.None).GetAwaiter().GetResult();
        ValidateAccountingWriteGuards();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ValidateCoordinatorWritesAsync(cancellationToken);
        ValidateAccountingWriteGuards();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task ValidateCoordinatorWritesAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries().Any(x => x.State is EntityState.Modified or EntityState.Deleted &&
            x.Entity is AuditEvent or AuditFieldChange or DeathRecipientPhoto))
            throw new InvalidOperationException("History and recipient photographs are append-only.");
        if (ChangeTracker.Entries<CoordinatorEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Coordinator history is append-only. Record a new transition instead.");

        // Revalidate only members whose eligibility/scope changed, plus changed positions.
        // A tracked position already ended by a lifecycle service has MemberId = null here.
        var memberIds = ChangeTracker.Entries<Member>().Where(x => x.State == EntityState.Modified &&
            (x.Property(m => m.Status).IsModified || x.Property(m => m.GroupNo).IsModified || x.Property(m => m.ArchivedAtUtc).IsModified))
            .Select(x => x.Entity.Id).ToArray();
        var positions = ChangeTracker.Entries<CoordinatorPosition>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified).Select(x => x.Entity).ToList();
        if (memberIds.Length > 0)
            positions.AddRange(await CoordinatorPositions.Where(x => x.MemberId != null && memberIds.Contains(x.MemberId.Value)).ToListAsync(ct).ConfigureAwait(false));

        foreach (var position in positions.Distinct())
        {
            if (position.MemberId is not { } memberId) continue;
            var member = await Members.FindAsync([memberId], ct).ConfigureAwait(false);
            if (member is null || member.Status != MemberStatus.Normal || member.ArchivedAtUtc is not null)
                throw new MemberValidationException("กรุณาสิ้นสุดหน้าที่ผู้ประสานงานก่อนเปลี่ยนสถานะหรือเก็บสมาชิกออกจากทะเบียน");
            if (position.RoleCode == CoordinatorRoles.GroupLeader && member.GroupNo != position.GroupNo)
                throw new MemberValidationException("หัวหน้ากลุ่มต้องอยู่ในกลุ่มที่รับผิดชอบ กรุณาสิ้นสุดหน้าที่ก่อนย้ายกลุ่ม");
        }
    }
}
