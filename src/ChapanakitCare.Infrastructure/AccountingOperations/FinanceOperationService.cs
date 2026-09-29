using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    private readonly AppDbContext database;
    public FinanceOperationService(AppDbContext database) { this.database = database; }

    public async Task<Guid> OpenAsync(OpenFinanceBook command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        var code = ParseBook(command.Book);
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        var fingerprint = Fingerprint("opening", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var setup = new AccountingSetupService(database);
        await setup.EnsureCatalogAsync(ct);
        var replay = await ReplayAsync(code, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var book = await database.AccountingBooks.SingleAsync(x => x.Code == code, ct);
        if (book.IsActivated)
        {
            throw new AccountingValidationException("สมุดบัญชีนี้เริ่มใช้งานแล้ว ให้บันทึกการแก้ไขพร้อมหลักฐานแทนการตั้งยอดใหม่");
        }

        var accounts = await database.AccountingAccounts.Where(x => x.BookId == book.Id).ToDictionaryAsync(x => x.Code, ct);
        var lines = new List<AccountingPostLine>();
        foreach (var amount in command.Amounts)
        {
            if (!accounts.TryGetValue(amount.AccountCode, out var account)
                || account.AccountType is AccountingAccountType.Income or AccountingAccountType.Expense
                || amount.DebitSatang < 0 || amount.CreditSatang < 0
                || (amount.DebitSatang == 0) == (amount.CreditSatang == 0))
                throw new AccountingValidationException("ยอดยกมาต้องใช้บัญชีสินทรัพย์ หนี้สิน หรือทุน และระบุเดบิตหรือเครดิตด้านเดียว");
            string? party = null;
            if (account.Role is AccountingAccountRole.MemberAdvanceLiability or AccountingAccountRole.MemberContributionShortfallReceivable)
            {
                var member = amount.MemberId is null ? null : await database.Members.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == amount.MemberId && x.ArchivedAtUtc == null, ct);
                if (member is null || (account.NormalBalance == AccountingNormalBalance.Credit ? amount.CreditSatang : amount.DebitSatang) <= 0)
                    throw new AccountingValidationException("ยอดเงินล่วงหน้าหรือยอดค้างชำระต้องระบุสมาชิกและยอดด้านปกติที่ถูกต้อง");
                party = $"{member.RunNo} {member.Title}{member.FirstName} {member.LastName}".Trim();
            }
            else if (account.Role == AccountingAccountRole.WelfareBenefitPayable)
            {
                var recipient = await database.DeathBeneficiarySnapshots.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.DeathCaseId == amount.DeathCaseId && x.SlotNo == amount.BeneficiarySlot, ct);
                if (recipient is null || amount.CreditSatang <= 0)
                    throw new AccountingValidationException("เงินสงเคราะห์ค้างจ่ายต้องระบุเหตุเสียชีวิต ผู้รับเงิน และยอดเครดิต");
                party = $"{recipient.Title}{recipient.FirstName} {recipient.LastName}".Trim();
            }
            else if (amount.MemberId is not null || amount.DeathCaseId is not null || amount.BeneficiarySlot is not null)
                throw new AccountingValidationException("รายละเอียดสมาชิกหรือผู้รับเงินไม่ตรงกับประเภทบัญชี");
            lines.Add(new(amount.AccountCode, amount.DebitSatang, amount.CreditSatang,
                new(amount.MemberId, amount.DeathCaseId, amount.BeneficiarySlot, PartySnapshot: party, DescriptionSnapshot: command.Evidence)));
        }
        if (lines.Count > 0 && Sum(lines.Select(x => x.DebitSatang)) != Sum(lines.Select(x => x.CreditSatang)))
            throw new AccountingValidationException("ยอดเดบิตและเครดิตของยอดยกมาต้องเท่ากัน กรุณาตรวจหลักฐาน");

        await setup.ActivateBookAsync(new(code, command.Date, command.Evidence, Actor(actor)), ct);
        var journalId = Guid.Empty;
        if (lines.Count > 0)
        {
            var posted = await new AccountingPostingService(database).PostAsync(new(code, "opening", command.Evidence,
                command.Date, command.RequestToken, fingerprint, Actor(actor), lines,
                new("opening", code.ToString(), JsonSerializer.Serialize(command))), ct);
            journalId = posted.JournalId;
        }
        else
        {
            var id = ZeroOpeningId(code, command.RequestToken);
            database.AuditEvents.Add(new AuditEvent
            {
                Id = id, OperationId = id, OccurredAtUtc = now, ActorUserId = "local-user", ActorDisplayName = actor,
                MachineName = Environment.MachineName, AppVersion = "accounting", Action = "accounting.zero_opening",
                EntityType = "accounting_book", EntityId = book.Id.ToString(),
                Reason = JsonSerializer.Serialize(new ZeroOpeningEvidence(fingerprint, command.Date, command.Evidence))
            });
        }
        if (code == AccountingBookCode.Welfare)
        {
            var rate = await database.SystemSettings.Select(x => x.WelfarePerMemberSatang).SingleAsync(ct);
            if (rate <= 0) throw new AccountingValidationException("อัตราเงินสงเคราะห์ต้องมากกว่าศูนย์");
            foreach (var member in await database.Members.Where(x => x.ArchivedAtUtc == null && x.Status == MemberStatus.Normal).ToListAsync(ct))
            {
                var balance = await new DeathAccountingService(database).GetAdvanceAsync(member.Id, ct) ?? 0;
                var units = checked((int)decimal.Floor((decimal)balance / rate));
                if (units == member.AdvanceUnitsBalance) continue;
                var order = checked((await database.AdvanceLedgerEntries.Where(x => x.MemberId == member.Id)
                    .MaxAsync(x => (long?)x.EntryOrder, ct) ?? 0) + 1);
                database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry
                {
                    Id = Guid.NewGuid(), MemberId = member.Id, EntryOrder = order, EntryType = "correction",
                    BusinessDate = command.Date, UnitsDelta = checked(units - member.AdvanceUnitsBalance),
                    BalanceBefore = member.AdvanceUnitsBalance, BalanceAfter = units,
                    Reason = $"ตั้งจำนวนหน่วยตามยอดยกมาที่ตรวจสอบ: {command.Evidence}", CreatedAtUtc = now, CreatedBy = actor
                });
                member.AdvanceUnitsBalance = units;
                member.Version++;
                member.UpdatedAtUtc = now;
                member.UpdatedBy = actor;
            }
            await database.SaveChangesAsync(ct);
        }
        await database.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return journalId;
    }

    internal static AccountingBookCode ParseBook(string book) => book.Trim().ToLowerInvariant() switch
    {
        "welfare" => AccountingBookCode.Welfare,
        "association" => AccountingBookCode.Association,
        _ => throw new AccountingValidationException("กรุณาเลือกสมุดบัญชีเงินสงเคราะห์หรือสมุดบัญชีสมาคม")
    };

    private static void ValidateEnvelope(DateOnly date, string evidence, string token, string actor)
    {
        if (date.Year is < 1900 or > 9456 || string.IsNullOrWhiteSpace(evidence) || evidence.Length > 2000
            || string.IsNullOrWhiteSpace(token) || token.Length > 100 || string.IsNullOrWhiteSpace(actor))
            throw new AccountingValidationException("กรุณาระบุวันที่ รายละเอียดหรือหลักฐาน และคำขอที่ถูกต้อง");
    }

    private static string Fingerprint<T>(string kind, T command) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Kind = kind, Command = command }))));
    private static AccountingActor Actor(string actor) => new("local-user", actor, Environment.MachineName, "accounting");
    private static long Sum(IEnumerable<long> values) => values.Aggregate(0L, (total, amount) => checked(total + amount));

    private async Task<Guid?> ReplayAsync(AccountingBookCode book, string token, string fingerprint, CancellationToken ct)
    {
        var journal = await (from j in database.AccountingJournals join b in database.AccountingBooks on j.BookId equals b.Id
                             where b.Code == book && j.RequestToken == token select j).SingleOrDefaultAsync(ct);
        if (journal is null)
        {
            var id = ZeroOpeningId(book, token);
            var opening = await database.AuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (opening is null) return null;
            var saved = JsonSerializer.Deserialize<ZeroOpeningEvidence>(opening.Reason ?? "null");
            if (saved?.RequestFingerprint != fingerprint)
                throw new AccountingIdempotencyConflictException("คำขอนี้เคยใช้เปิดบัญชีด้วยข้อมูลอื่นแล้ว กรุณาเปิดแบบฟอร์มใหม่");
            return Guid.Empty;
        }
        if (journal.RequestFingerprint != fingerprint)
            throw new AccountingIdempotencyConflictException("คำขอนี้เคยบันทึกด้วยข้อมูลอื่น กรุณาเปิดแบบฟอร์มใหม่");
        return journal.Id;
    }

    private static Guid ZeroOpeningId(AccountingBookCode book, string token) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"accounting.zero_opening:{book}:{token}")).AsSpan(0, 16));
    private sealed record ZeroOpeningEvidence(string RequestFingerprint, DateOnly Date, string Evidence);
}
