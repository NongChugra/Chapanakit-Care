using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    public async Task<MoneyReconciliation> ReconcileAsync(ReconcileMoneyAccount command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        var code = ParseBook(command.Book);
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        var fingerprint = Fingerprint("reconciliation", command);
        var identity = SHA256.HashData(Encoding.UTF8.GetBytes($"accounting.reconciled:{code}:{command.RequestToken}"));
        var id = new Guid(identity.AsSpan(0, 16));
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var existing = await database.AuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (existing is not null)
        {
            var saved = JsonSerializer.Deserialize<MoneyReconciliation>(existing.Reason ?? "")
                ?? throw new AccountingValidationException("หลักฐานกระทบยอดเดิมไม่ครบถ้วน");
            if (saved.RequestFingerprint != fingerprint)
                throw new AccountingIdempotencyConflictException("คำขอนี้เคยกระทบยอดด้วยข้อมูลอื่นแล้ว");
            return saved;
        }
        var accounts = await BookAccountsAsync(code, ct);
        var account = MoneyAccount(accounts, command.AccountCode);
        var book = await database.AccountingBooks.AsNoTracking().SingleAsync(x => x.Id == account.BookId, ct);
        if (command.Date < book.CutoverStartDate)
            throw new AccountingValidationException("วันที่กระทบยอดต้องไม่ก่อนวันเปิดบัญชี");
        var lines = await (from line in database.AccountingJournalLines
            join journal in database.AccountingJournals on line.JournalId equals journal.Id
            where line.AccountId == account.Id && journal.BusinessDate <= command.Date
            select new { line.DebitSatang, line.CreditSatang }).ToListAsync(ct);
        var balance = Sum(lines.Select(x => checked(x.DebitSatang - x.CreditSatang)));
        var result = new MoneyReconciliation(id, command.Book, command.AccountCode, command.Date, balance,
            command.StatementBalanceSatang, checked(command.StatementBalanceSatang - balance), command.Evidence, fingerprint);
        database.AuditEvents.Add(new AuditEvent
        {
            Id = id, OperationId = id, OccurredAtUtc = now, ActorUserId = "local-user", ActorDisplayName = actor,
            MachineName = Environment.MachineName, AppVersion = "accounting", Action = "accounting.reconciled",
            EntityType = "accounting_account", EntityId = account.Id.ToString(), Reason = JsonSerializer.Serialize(result)
        });
        await database.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
