using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    public async Task<Guid> RefundAsync(RefundMemberAdvance command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        if (command.AmountSatang <= 0) throw new AccountingValidationException("จำนวนเงินต้องมากกว่าศูนย์");
        var fingerprint = Fingerprint("member_refund", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(AccountingBookCode.Welfare, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var accounts = await BookAccountsAsync(AccountingBookCode.Welfare, ct);
        var cash = MoneyAccount(accounts, command.MoneyAccount);
        var member = await database.Members.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.MemberId && x.ArchivedAtUtc == null, ct)
            ?? throw new AccountingValidationException("ไม่พบสมาชิก");
        if (member.Status == MemberStatus.Normal)
            throw new AccountingValidationException("จ่ายคืนเงินคงเหลือได้หลังลาออกหรือยืนยันการเสียชีวิตแล้ว");
        var advanceId = accounts["2000"].Id;
        var debtId = accounts["1200"].Id;
        var lines = await database.AccountingJournalLines.AsNoTracking().Where(x => x.MemberId == member.Id
            && (x.AccountId == advanceId || x.AccountId == debtId)).ToListAsync(ct);
        var advance = Sum(lines.Where(x => x.AccountId == advanceId).Select(x => checked(x.CreditSatang - x.DebitSatang)));
        var debt = Sum(lines.Where(x => x.AccountId == debtId).Select(x => checked(x.DebitSatang - x.CreditSatang)));
        if (debt != 0 || command.AmountSatang > advance)
            throw new AccountingValidationException("ต้องชำระยอดค้างก่อน และจำนวนเงินคืนต้องไม่เกินเงินล่วงหน้าคงเหลือ");
        await RequireCashAsync(cash, command.AmountSatang, ct);
        var dimensions = new AccountingDimensions(MemberId: member.Id,
            PartySnapshot: $"{member.RunNo} {member.Title}{member.FirstName} {member.LastName}", DescriptionSnapshot: command.Evidence);
        var posted = await new AccountingPostingService(database).PostAsync(new(AccountingBookCode.Welfare, "member_refund",
            command.Evidence, command.Date, command.RequestToken, fingerprint, Actor(actor),
            [new("2000", command.AmountSatang, 0, dimensions), new(command.MoneyAccount, 0, command.AmountSatang, dimensions)],
            new("member_refund", command.RequestToken, JsonSerializer.Serialize(command))), ct);
        await tx.CommitAsync(ct);
        return posted.JournalId;
    }
}
