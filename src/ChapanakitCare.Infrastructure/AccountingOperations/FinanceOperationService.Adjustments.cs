using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    public async Task<Guid> ReclassifyExpenseAsync(ReclassifyExpense command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        if (command.AmountSatang <= 0 || command.FromExpenseAccount == command.ToExpenseAccount)
            throw new AccountingValidationException("จำนวนเงินต้องเป็นบวกและหมวดเดิมกับหมวดที่ถูกต้องต้องต่างกัน");
        var fingerprint = Fingerprint("expense_reclassification", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(AccountingBookCode.Association, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var accounts = await BookAccountsAsync(AccountingBookCode.Association, ct);
        if (!accounts.TryGetValue(command.FromExpenseAccount, out var sourceAccount) || sourceAccount.Role != AccountingAccountRole.OperatingExpense
            || !accounts.TryGetValue(command.ToExpenseAccount, out var to) || to.Role != AccountingAccountRole.OperatingExpense)
            throw new AccountingValidationException("การแก้หมวดใช้ได้เฉพาะหมวดค่าใช้จ่ายสมาคม ไม่เปลี่ยนเงินสดหรือรายได้");
        var original = await database.AccountingJournals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.OriginalJournalId && x.BookId == sourceAccount.BookId, ct);
        if (original is null || original.VoucherType is not ("operating_expense" or "expense_reclassification")
            || command.Date < original.BusinessDate || await database.AccountingJournals.AnyAsync(x => x.ReversesJournalId == original.Id, ct))
            throw new AccountingValidationException("เลือกใบจ่ายหรือใบแก้หมวดที่ยังไม่ยกเลิก และวันที่แก้ต้องไม่ก่อนเอกสารเดิม");
        var originalLines = await database.AccountingJournalLines.AsNoTracking().Where(x => x.JournalId == original.Id && x.AccountId == sourceAccount.Id).ToListAsync(ct);
        var sourceAmount = Sum(originalLines.Select(x => checked(x.DebitSatang - x.CreditSatang)));
        var sourceId = original.Id.ToString();
        var priorCorrections = await (from journal in database.AccountingJournals.AsNoTracking()
            join line in database.AccountingJournalLines.AsNoTracking() on journal.Id equals line.JournalId
            where journal.BookId == sourceAccount.BookId && journal.SourceType == "expense_reclassification" && journal.SourceId == sourceId && line.AccountId == sourceAccount.Id
            select new { line.DebitSatang, line.CreditSatang }).ToListAsync(ct);
        var alreadyMoved = Sum(priorCorrections.Select(x => checked(x.CreditSatang - x.DebitSatang)));
        if (command.AmountSatang > checked(sourceAmount - alreadyMoved))
            throw new AccountingValidationException("จำนวนเงินเกินส่วนของหมวดเดิมที่ยังไม่ได้แก้ไขในเอกสารนี้");
        var dimensions = new AccountingDimensions(PartySnapshot: originalLines.Select(x => x.PartySnapshot).FirstOrDefault(x => x is not null),
            DescriptionSnapshot: command.Evidence);
        var posted = await new AccountingPostingService(database).PostAsync(new(AccountingBookCode.Association, "expense_reclassification",
            command.Evidence, command.Date, command.RequestToken, fingerprint, Actor(actor),
            [new(command.ToExpenseAccount, command.AmountSatang, 0, dimensions), new(command.FromExpenseAccount, 0, command.AmountSatang, dimensions)],
            new("expense_reclassification", sourceId, JsonSerializer.Serialize(command))), ct);
        await tx.CommitAsync(ct);
        return posted.JournalId;
    }
}
