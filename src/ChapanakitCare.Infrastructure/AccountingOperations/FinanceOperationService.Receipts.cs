using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    public async Task<Guid> ReceiveAsync(ReceiveWelfareMoney command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Description, command.RequestToken, actor);
        if (command.Members is null || command.Members.Count == 0
            || command.Members.Any(x => x.AmountSatang <= 0)
            || command.Members.Select(x => x.MemberId).Distinct().Count() != command.Members.Count)
            throw new AccountingValidationException("กรุณาระบุสมาชิกไม่ซ้ำและจำนวนเงินบวก");

        var fingerprint = Fingerprint("receipt", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(AccountingBookCode.Welfare, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var book = await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == AccountingBookCode.Welfare, ct);
        if (book is null || !book.IsActivated)
            throw new AccountingValidationException("กรุณาเปิดบัญชีเงินสงเคราะห์ด้วยยอดยกมาก่อนรับเงิน");
        var accounts = await database.AccountingAccounts.Where(x => x.BookId == book.Id).ToDictionaryAsync(x => x.Code, ct);
        if (!accounts.TryGetValue(command.MoneyAccount, out var cash)
            || cash.Role is not (AccountingAccountRole.Cash or AccountingAccountRole.Bank))
            throw new AccountingValidationException("บัญชีรับเงินต้องเป็นเงินสดหรือธนาคารของบัญชีเงินสงเคราะห์");

        var lines = new List<AccountingPostLine> { new(command.MoneyAccount, Sum(command.Members.Select(x => x.AmountSatang)), 0) };
        var projections = new List<(Member Member, int Units)>();
        var settings = await database.SystemSettings.SingleAsync(ct);
        if (settings.WelfarePerMemberSatang <= 0)
            throw new AccountingValidationException("อัตราเงินสงเคราะห์ต้องมากกว่าศูนย์");
        foreach (var receipt in command.Members)
        {
            var member = await database.Members.SingleOrDefaultAsync(x => x.Id == receipt.MemberId
                && x.ArchivedAtUtc == null, ct)
                ?? throw new AccountingValidationException("ไม่พบสมาชิกที่ยังใช้งานอยู่");
            if (receipt.CollectionId is { } collectionId)
            {
                var collection = await database.Set<WelfareCollection>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == collectionId, ct);
                if (collection is null || collection.MemberId != member.Id || command.Date < collection.BusinessDate)
                    throw new AccountingValidationException("ใบเรียกเก็บต้องตรงกับสมาชิกและไม่เกิดหลังวันที่รับเงิน");
                var allocations = await database.AccountingJournalLines.Where(x => x.BookId == book.Id
                    && x.CollectionRequestId == collectionId).ToListAsync(ct);
                var paid = Sum(allocations.Select(x => checked(x.CreditSatang - x.DebitSatang)));
                if (paid < 0 || receipt.AmountSatang > checked(collection.AmountSatang - paid))
                    throw new AccountingValidationException("จำนวนเงินเกินยอดค้างของใบเรียกเก็บ ให้แยกเงินล่วงหน้าส่วนเกินเป็นรายการรับเงินใหม่");
            }
            var memberLines = await database.AccountingJournalLines.Where(x => x.BookId == book.Id && x.MemberId == member.Id).ToListAsync(ct);
            var advance = Sum(memberLines.Where(x => x.AccountId == accounts["2000"].Id).Select(x => checked(x.CreditSatang - x.DebitSatang)));
            var debt = Sum(memberLines.Where(x => x.AccountId == accounts["1200"].Id).Select(x => checked(x.DebitSatang - x.CreditSatang)));
            if (advance < 0 || debt < 0)
                throw new AccountingValidationException("ยอดสมาชิกผิดด้าน กรุณาตรวจสอบบัญชีก่อนรับเงิน");
            if (member.Status != MemberStatus.Normal && receipt.AmountSatang > debt)
                throw new AccountingValidationException("สมาชิกลาออกหรือเสียชีวิตรับชำระได้ไม่เกินยอดค้าง ไม่รับเงินล่วงหน้าใหม่");
            var cleared = Math.Min(debt, receipt.AmountSatang);
            var prepaid = receipt.AmountSatang - cleared;
            var dimensions = new AccountingDimensions(MemberId: member.Id, CollectionRequestId: receipt.CollectionId,
                PartySnapshot: $"{member.RunNo} {member.Title}{member.FirstName} {member.LastName} กลุ่ม {member.GroupNo}",
                DescriptionSnapshot: command.Description);
            if (cleared > 0) lines.Add(new("1200", 0, cleared, dimensions));
            if (prepaid > 0) lines.Add(new("2000", 0, prepaid, dimensions));

            var net = checked(advance + prepaid - (debt - cleared));
            var units = checked((int)decimal.Floor((decimal)net / settings.WelfarePerMemberSatang));
            if (member.Status == MemberStatus.Normal) projections.Add((member, units));
        }
        var posted = await new AccountingPostingService(database).PostAsync(new(AccountingBookCode.Welfare,
            "advance_receipt", command.Description, command.Date, command.RequestToken, fingerprint, Actor(actor), lines,
            new("member_receipt", command.RequestToken, JsonSerializer.Serialize(command))), ct);
        foreach (var (member, units) in projections)
        {
            if (units != member.AdvanceUnitsBalance)
            {
                var order = checked((await database.AdvanceLedgerEntries.Where(x => x.MemberId == member.Id)
                    .MaxAsync(x => (long?)x.EntryOrder, ct) ?? 0) + 1);
                database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry
                {
                    Id = Guid.NewGuid(), MemberId = member.Id, EntryOrder = order, EntryType = "correction",
                    BusinessDate = command.Date, UnitsDelta = checked(units - member.AdvanceUnitsBalance),
                    BalanceBefore = member.AdvanceUnitsBalance, BalanceAfter = units,
                    Reason = $"ปรับจำนวนหน่วยตามยอดเงินจริงหลังรับเงิน: {command.RequestToken}", CreatedAtUtc = now, CreatedBy = actor
                });
                member.AdvanceUnitsBalance = units;
                member.Version++;
                member.UpdatedAtUtc = now;
                member.UpdatedBy = actor;
            }
        }
        await database.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return posted.JournalId;
    }
}
