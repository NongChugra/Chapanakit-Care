using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed record WelfareCollectionLine(Guid MemberId, long AmountSatang);
public sealed record CreateWelfareCollection(string CycleKey, DateOnly BusinessDate, DateOnly DueDate,
    string Description, string RequestToken, IReadOnlyList<WelfareCollectionLine> Lines);

public sealed class WelfareCollectionService(AppDbContext database)
{
    public async Task<IReadOnlyList<WelfareCollection>> CreateAsync(CreateWelfareCollection command,
        DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.CycleKey) || command.CycleKey.Length > 100
            || string.IsNullOrWhiteSpace(command.Description) || command.Description.Length > 1000
            || string.IsNullOrWhiteSpace(command.RequestToken) || command.RequestToken.Length > 100
            || string.IsNullOrWhiteSpace(actor) || command.BusinessDate.Year is < 1900 or > 9456
            || command.DueDate < command.BusinessDate || command.DueDate.Year > 9456
            || command.Lines.Count == 0 || command.Lines.Any(x => x.AmountSatang <= 0)
            || command.Lines.Select(x => x.MemberId).Distinct().Count() != command.Lines.Count)
            throw new MemberValidationException("กรุณาตรวจสอบรอบเรียกเก็บ วันที่ จำนวนเงิน และรายชื่อสมาชิก");

        var ordered = command.Lines.OrderBy(x => x.MemberId).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Cycle = command.CycleKey.Trim(), command.BusinessDate, command.DueDate,
            Description = command.Description.Trim(), Lines = ordered
        }))));
        await using var tx = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(ct) : null;
        var existing = await database.Set<WelfareCollection>().Where(x => x.RequestToken == command.RequestToken).ToListAsync(ct);
        if (existing.Count > 0)
        {
            if (existing.Count != ordered.Length || existing.Any(x => x.RequestFingerprint != fingerprint))
                throw new MemberValidationException("คำขอนี้เคยใช้กับข้อมูลอื่น กรุณาเปิดแบบฟอร์มใหม่");
            return existing.OrderBy(x => x.MemberRunNo).ToArray();
        }

        var ids = ordered.Select(x => x.MemberId).ToArray();
        var members = await database.Members.Where(x => ids.Contains(x.Id)
            && x.Status == MemberStatus.Normal && x.ArchivedAtUtc == null).ToDictionaryAsync(x => x.Id, ct);
        if (members.Count != ids.Length)
            throw new MemberValidationException("มีสมาชิกที่ไม่อยู่ในสถานะปกติหรือไม่พบในทะเบียน กรุณาตรวจสอบรายชื่อ");
        if (await database.Set<WelfareCollection>().AnyAsync(x => ids.Contains(x.MemberId) && x.CycleKey == command.CycleKey.Trim(), ct))
            throw new MemberValidationException("มีใบเรียกเก็บสำหรับสมาชิกในรอบนี้แล้ว กรุณาตรวจสอบรายการเดิม");

        var rows = ordered.Select(line =>
        {
            var member = members[line.MemberId];
            return new WelfareCollection
            {
                Id = Guid.NewGuid(), MemberId = member.Id, MemberRunNo = member.RunNo,
                MemberName = $"{member.Title}{member.FirstName} {member.LastName}".Trim(), GroupNo = member.GroupNo ?? string.Empty,
                CycleKey = command.CycleKey.Trim(), BusinessDate = command.BusinessDate, DueDate = command.DueDate,
                AmountSatang = line.AmountSatang, RequestToken = command.RequestToken, RequestFingerprint = fingerprint,
                Description = command.Description.Trim(), CreatedAtUtc = now, CreatedBy = actor
            };
        }).ToArray();
        database.AddRange(rows);
        database.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), OccurredAtUtc = now,
            ActorUserId = "local-user", ActorDisplayName = actor, MachineName = Environment.MachineName,
            AppVersion = "accounting", Action = "finance.collection.created", EntityType = "welfare_collection",
            EntityId = command.RequestToken, Reason = $"{command.CycleKey.Trim()} · {rows.Length} ราย"
        });
        await database.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return rows.OrderBy(x => x.MemberRunNo).ToArray();
    }
}
