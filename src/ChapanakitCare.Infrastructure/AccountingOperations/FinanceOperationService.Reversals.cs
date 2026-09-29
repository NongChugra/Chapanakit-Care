using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    public async Task<Guid> ReverseAsync(ReverseFinanceDocument command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Reason, command.RequestToken, actor);
        var fingerprint = Fingerprint("reversal", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var original = await database.AccountingJournals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.JournalId, ct)
            ?? throw new AccountingValidationException("ไม่พบรายการต้นฉบับ");
        var books = await database.AccountingBooks.ToDictionaryAsync(x => x.Id, ct);
        var replay = await ReplayAsync(books[original.BookId].Code, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var originals = original.LinkedOperationId is { } linked
            ? await database.AccountingJournals.AsNoTracking().Where(x => x.LinkedOperationId == linked).ToListAsync(ct)
            : [original];
        if (original.LinkedOperationId is not null && (originals.Count != 2 || originals.Any(x => x.VoucherType != "fee_remittance")))
            throw new AccountingValidationException("ยกเลิกได้เฉพาะคู่รายการนำส่งค่าหักที่ตรวจสอบแล้ว");

        var requests = new List<AccountingPostRequest>();
        var members = new HashSet<Guid>();
        foreach (var journal in originals)
        {
            if (journal.ReversesJournalId is not null || journal.VoucherType is not
                ("advance_receipt" or "operating_expense" or "cash_transfer" or "benefit_payment" or "fee_remittance" or "member_refund")
                || await database.AccountingJournals.AnyAsync(x => x.ReversesJournalId == journal.Id, ct))
                throw new AccountingValidationException("รายการนี้ยกเลิกแล้ว หรือเป็นยอดยกมา/การตั้งหนี้ที่ต้องแก้ไขผ่านกระบวนการเฉพาะ");
            var book = books[journal.BookId];
            if (journal.JournalNumber != $"{book.JournalPrefix}-{book.NextJournalNumber - 1:00000000}" || command.Date < journal.BusinessDate)
                throw new AccountingValidationException("มีรายการบัญชีภายหลังแล้ว หรือวันที่ยกเลิกก่อนต้นฉบับ กรุณาตรวจสอบรายการที่เกี่ยวข้องก่อน");
            var accounts = await database.AccountingAccounts.Where(x => x.BookId == book.Id).ToDictionaryAsync(x => x.Id, ct);
            var lines = await database.AccountingJournalLines.AsNoTracking().Where(x => x.JournalId == journal.Id).OrderBy(x => x.LineNo).ToListAsync(ct);
            foreach (var line in lines.Where(x => x.MemberId != null)) members.Add(line.MemberId!.Value);
            requests.Add(new(book.Code, "reversal", command.Reason, command.Date, command.RequestToken, fingerprint, Actor(actor),
                lines.Select(line => new AccountingPostLine(accounts[line.AccountId].Code, line.CreditSatang, line.DebitSatang,
                    new(line.MemberId, line.DeathCaseId, line.BeneficiarySlotNo, line.CollectionRequestId, line.PartySnapshot, line.DescriptionSnapshot))).ToList(),
                new("reversal", journal.Id.ToString(), JsonSerializer.Serialize(command)), journal.Id));
        }
        var posting = new AccountingPostingService(database);
        Guid id;
        if (requests.Count == 2)
        {
            var pair = await posting.PostLinkedAsync(new(Guid.NewGuid(), requests.Single(x => x.BookCode == AccountingBookCode.Welfare),
                requests.Single(x => x.BookCode == AccountingBookCode.Association)), ct);
            id = books[original.BookId].Code == AccountingBookCode.Welfare ? pair.WelfarePosting.JournalId : pair.AssociationPosting.JournalId;
        }
        else id = (await posting.PostAsync(requests[0], ct)).JournalId;

        if (members.Count > 0)
        {
            var rate = await database.SystemSettings.Select(x => x.WelfarePerMemberSatang).SingleAsync(ct);
            foreach (var memberId in members)
            {
                var member = await database.Members.SingleAsync(x => x.Id == memberId, ct);
                if (member.Status != MemberStatus.Normal) continue;
                var balance = await new DeathAccountingService(database).GetAdvanceAsync(memberId, ct) ?? 0;
                var units = checked((int)decimal.Floor((decimal)balance / rate));
                if (units == member.AdvanceUnitsBalance) continue;
                database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry
                {
                    Id = Guid.NewGuid(), MemberId = memberId,
                    EntryOrder = checked((await database.AdvanceLedgerEntries.Where(x => x.MemberId == memberId).MaxAsync(x => (long?)x.EntryOrder, ct) ?? 0) + 1),
                    EntryType = "correction", BusinessDate = command.Date, UnitsDelta = checked(units - member.AdvanceUnitsBalance),
                    BalanceBefore = member.AdvanceUnitsBalance, BalanceAfter = units,
                    Reason = $"ยกเลิกรายการ {original.JournalNumber}: {command.Reason}", CreatedAtUtc = now, CreatedBy = actor
                });
                member.AdvanceUnitsBalance = units;
                member.Version++;
                member.UpdatedAtUtc = now;
                member.UpdatedBy = actor;
            }
            await database.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return id;
    }
}
