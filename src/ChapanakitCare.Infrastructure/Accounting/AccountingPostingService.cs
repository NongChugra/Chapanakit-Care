using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChapanakitCare.Infrastructure.Accounting;

public sealed class AccountingPostingService : IAccountingPostingService
{
    private readonly AppDbContext database;

    public AccountingPostingService(AppDbContext database)
    {
        this.database = database;
    }

    public async Task<AccountingPostingResult> PostAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var existing = await FindExistingAsync(request, cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestFingerprint == request.RequestFingerprint)
                    return new AccountingPostingResult(existing.Id, existing.JournalNumber, true);
                throw new AccountingIdempotencyConflictException("รหัสคำขอนี้ถูกใช้กับข้อมูลบัญชีที่ต่างกันแล้ว");
            }

            var result = await PostNewAsync(request, linkedOperationId: null, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<AccountingLinkedPostingResult> PostLinkedAsync(
        AccountingLinkedPostRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateLinkedRequest(request);
        var trackerCheckpoint = ChangeTrackerCheckpoint.Capture(database);
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        string? savepointName = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            else
            {
                transaction = database.Database.CurrentTransaction!;
                if (!transaction.SupportsSavepoints)
                    throw new AccountingValidationException("ผู้ให้บริการฐานข้อมูลไม่รองรับจุดบันทึกสำหรับรายการบัญชีร่วม");

                savepointName = $"accounting_linked_{Guid.NewGuid():N}";
                await transaction.CreateSavepointAsync(savepointName, cancellationToken);
            }

            var welfareExisting = await FindExistingAsync(request.WelfarePosting, cancellationToken);
            var associationExisting = await FindExistingAsync(request.AssociationPosting, cancellationToken);
            if (welfareExisting is not null || associationExisting is not null)
            {
                var replay = ReplayLinkedPair(request, welfareExisting, associationExisting);
                if (savepointName is not null)
                    await transaction!.ReleaseSavepointAsync(savepointName, cancellationToken);
                return replay;
            }

            var welfare = await PostNewAsync(request.WelfarePosting, request.LinkedOperationId, cancellationToken);
            var association = await PostNewAsync(request.AssociationPosting, request.LinkedOperationId, cancellationToken);
            if (transaction is not null)
            {
                if (ownsTransaction)
                    await transaction.CommitAsync(cancellationToken);
                else if (savepointName is not null)
                    await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
            }
            return new AccountingLinkedPostingResult(welfare, association, false);
        }
        catch
        {
            try
            {
                if (transaction is not null)
                {
                    if (ownsTransaction)
                        await transaction.RollbackAsync(cancellationToken);
                    else if (savepointName is not null)
                        await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                }
            }
            finally
            {
                trackerCheckpoint.Restore(database);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public Task<AccountingPostingResult> ReverseAsync(
        AccountingReverseRequest request,
        CancellationToken cancellationToken = default) =>
        ReverseCoreAsync(request, cancellationToken);

    private async Task<AccountingPostingResult> PostNewAsync(
        AccountingPostRequest request,
        Guid? linkedOperationId,
        CancellationToken cancellationToken)
    {
        var book = await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == request.BookCode, cancellationToken)
            ?? throw new AccountingValidationException("ยังไม่ได้จัดเตรียมผังบัญชี");
        if (!book.IsActivated || book.CutoverStartDate is null)
            throw new AccountingValidationException("สมุดบัญชียังไม่เปิดใช้งานด้วยยอดยกมาตามหลักฐาน");
        if (request.BusinessDate < book.CutoverStartDate.Value)
            throw new AccountingValidationException("วันที่รายการก่อนวันตัดยอดของสมุดบัญชี");

        if (await database.AccountingPeriods.AnyAsync(x => x.BookId == book.Id &&
            x.Status == AccountingPeriodStatus.Closed && x.StartsOn <= request.BusinessDate &&
            request.BusinessDate <= x.EndsOn, cancellationToken))
            throw new AccountingPeriodClosedException("งวดบัญชีของวันที่รายการนี้ปิดแล้ว");

        var latestDate = await database.AccountingJournals.Where(x => x.BookId == book.Id)
            .OrderByDescending(x => x.BusinessDate).Select(x => (DateOnly?)x.BusinessDate).FirstOrDefaultAsync(cancellationToken);
        if (latestDate is not null && request.BusinessDate < latestDate.Value)
            throw new AccountingValidationException("ไม่สามารถบันทึกรายการย้อนหลังจากรายการบัญชีล่าสุดได้");

        var accountCodes = request.Lines.Select(x => x.AccountCode.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        var accounts = await database.AccountingAccounts.Where(x => x.BookId == book.Id && accountCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);
        if (accounts.Count != accountCodes.Length)
            throw new AccountingValidationException("ไม่พบบัญชีที่เลือกในสมุดบัญชีนี้");
        if (accounts.Any(x => x.AccountType == AccountingAccountType.Income &&
            (book.Code != AccountingBookCode.Association || x.Code != "4000" || x.Role != AccountingAccountRole.WelfareDeductionIncome)))
            throw new AccountingValidationException("รายได้ของสมาคมบันทึกได้เฉพาะบัญชี 4000 ค่าหักเงินสงเคราะห์ร้อยละ 4");

        var now = DateTimeOffset.UtcNow;
        var operationId = linkedOperationId ?? Guid.NewGuid();
        var journal = new AccountingJournal
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            OperationId = operationId,
            LinkedOperationId = linkedOperationId,
            JournalNumber = $"{book.JournalPrefix}-{book.NextJournalNumber:00000000}",
            VoucherType = request.VoucherType.Trim(),
            DescriptionSnapshot = request.Description.Trim(),
            BusinessDate = request.BusinessDate,
            RecordedAtUtc = now,
            RequestToken = request.RequestToken.Trim(),
            RequestFingerprint = request.RequestFingerprint.Trim(),
            ReversesJournalId = request.ReversesJournalId,
            SourceType = request.Source?.SourceType.Trim(),
            SourceId = request.Source?.SourceId.Trim(),
            SourceSnapshot = request.Source?.SourceSnapshot,
            ActorUserId = request.Actor.UserId.Trim(),
            ActorDisplayName = request.Actor.DisplayName.Trim(),
            MachineName = request.Actor.MachineName.Trim(),
            AppVersion = request.Actor.AppVersion.Trim()
        };
        book.NextJournalNumber++;
        book.Version++;
        var accountByCode = accounts.ToDictionary(x => x.Code, StringComparer.Ordinal);
        var lines = request.Lines.Select((line, index) => new AccountingJournalLine
        {
            Id = Guid.NewGuid(),
            JournalId = journal.Id,
            BookId = book.Id,
            AccountId = accountByCode[line.AccountCode.Trim()].Id,
            LineNo = index + 1,
            DebitSatang = line.DebitSatang,
            CreditSatang = line.CreditSatang,
            MemberId = line.Dimensions?.MemberId,
            DeathCaseId = line.Dimensions?.DeathCaseId,
            BeneficiarySlotNo = line.Dimensions?.BeneficiarySlotNo,
            CollectionRequestId = line.Dimensions?.CollectionRequestId,
            PartySnapshot = line.Dimensions?.PartySnapshot,
            DescriptionSnapshot = line.Dimensions?.DescriptionSnapshot
        }).ToList();
        database.AccountingJournals.Add(journal);
        database.AccountingJournalLines.AddRange(lines);
        database.AccountingPostingAudits.Add(new AccountingPostingAudit
        {
            Id = Guid.NewGuid(),
            JournalId = journal.Id,
            OperationId = operationId,
            OccurredAtUtc = now,
            Action = "posted",
            ActorUserId = journal.ActorUserId,
            ActorDisplayName = journal.ActorDisplayName,
            MachineName = journal.MachineName,
            AppVersion = journal.AppVersion,
            DetailsSnapshot = journal.DescriptionSnapshot
        });
        await database.SaveChangesAsync(cancellationToken);
        return new AccountingPostingResult(journal.Id, journal.JournalNumber, false);
    }

    private static void ValidateRequest(AccountingPostRequest request)
    {
        if (!Enum.IsDefined(request.BookCode))
            throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก");
        if (string.IsNullOrWhiteSpace(request.VoucherType) || string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.RequestToken) || string.IsNullOrWhiteSpace(request.RequestFingerprint))
            throw new AccountingValidationException("ต้องระบุประเภท คำอธิบาย รหัสคำขอ และลายนิ้วมือคำขอของรายการบัญชี");
        if (string.IsNullOrWhiteSpace(request.Actor.UserId) || string.IsNullOrWhiteSpace(request.Actor.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Actor.MachineName) || string.IsNullOrWhiteSpace(request.Actor.AppVersion))
            throw new AccountingValidationException("ต้องระบุผู้บันทึก เครื่อง และรุ่นโปรแกรมสำหรับรายการบัญชี");
        if (request.Lines is null || request.Lines.Count < 2)
            throw new AccountingValidationException("รายการบัญชีต้องมีอย่างน้อยสองบรรทัด");

        long totalDebit = 0;
        long totalCredit = 0;
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.AccountCode) ||
                line.DebitSatang < 0 ||
                line.CreditSatang < 0 ||
                (line.DebitSatang <= 0 && line.CreditSatang <= 0) ||
                (line.DebitSatang > 0 && line.CreditSatang > 0))
                throw new AccountingValidationException("แต่ละบรรทัดต้องระบุบัญชีและเดบิตหรือเครดิตจำนวนบวกเพียงด้านเดียว");
            if (line.Dimensions?.BeneficiarySlotNo is { } slot && slot is not (1 or 2))
                throw new AccountingValidationException("ผู้รับผลประโยชน์ต้องเป็นลำดับ 1 หรือ 2");
            totalDebit = checked(totalDebit + line.DebitSatang);
            totalCredit = checked(totalCredit + line.CreditSatang);
        }

        if (totalDebit != totalCredit)
            throw new AccountingValidationException("เดบิตและเครดิตของรายการบัญชีต้องเท่ากัน");
        if (request.Source is not null && (string.IsNullOrWhiteSpace(request.Source.SourceType) || string.IsNullOrWhiteSpace(request.Source.SourceId)))
            throw new AccountingValidationException("แหล่งอ้างอิงรายการบัญชีต้องมีทั้งประเภทและรหัส");
    }

    private async Task<AccountingPostingResult> ReverseCoreAsync(
        AccountingReverseRequest request,
        CancellationToken cancellationToken)
    {
        ValidateReverseRequest(request);
        var trackerCheckpoint = ChangeTrackerCheckpoint.Capture(database);
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        string? savepointName = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            else
            {
                transaction = database.Database.CurrentTransaction!;
                if (!transaction.SupportsSavepoints)
                    throw new AccountingValidationException("ผู้ให้บริการฐานข้อมูลไม่รองรับจุดบันทึกสำหรับการยกเลิกรายการบัญชี");

                savepointName = $"accounting_reversal_{Guid.NewGuid():N}";
                await transaction.CreateSavepointAsync(savepointName, cancellationToken);
            }

            var original = await database.AccountingJournals.SingleOrDefaultAsync(
                journal => journal.Id == request.OriginalJournalId,
                cancellationToken) ?? throw new AccountingValidationException("ไม่พบรายการบัญชีต้นฉบับ");
            var book = await database.AccountingBooks.SingleAsync(x => x.Id == original.BookId, cancellationToken);
            var existing = await database.AccountingJournals.SingleOrDefaultAsync(journal =>
                journal.BookId == original.BookId && journal.RequestToken == request.RequestToken.Trim(),
                cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestFingerprint == request.RequestFingerprint.Trim() &&
                    existing.ReversesJournalId == original.Id)
                {
                    if (savepointName is not null)
                        await transaction!.ReleaseSavepointAsync(savepointName, cancellationToken);
                    return new AccountingPostingResult(existing.Id, existing.JournalNumber, true);
                }

                throw new AccountingIdempotencyConflictException("รหัสคำขอยกเลิกนี้ถูกใช้กับข้อมูลบัญชีที่ต่างกันแล้ว");
            }

            if (original.LinkedOperationId is not null || original.ReversesJournalId is not null ||
                original.VoucherType is "opening" or "death_accrual")
                throw new AccountingValidationException("รายการนี้ต้องแก้ไขผ่านกระบวนการเฉพาะและไม่สามารถยกเลิกรายการเดี่ยวได้");
            if (request.BusinessDate < original.BusinessDate)
                throw new AccountingValidationException("วันที่ยกเลิกต้องไม่ก่อนวันที่ของรายการต้นฉบับ");
            if (await database.AccountingJournals.AnyAsync(journal => journal.ReversesJournalId == original.Id, cancellationToken))
                throw new AccountingValidationException("รายการบัญชีต้นฉบับนี้ถูกยกเลิกแล้ว");

            var accounts = await database.AccountingAccounts.Where(account => account.BookId == book.Id)
                .ToDictionaryAsync(account => account.Id, cancellationToken);
            var originalLines = await database.AccountingJournalLines.Where(line => line.JournalId == original.Id)
                .OrderBy(line => line.LineNo).ToListAsync(cancellationToken);
            if (originalLines.Count < 2 || originalLines.Any(line => !accounts.ContainsKey(line.AccountId)))
                throw new AccountingValidationException("ข้อมูลบรรทัดรายการต้นฉบับไม่สมบูรณ์และไม่สามารถยกเลิกได้");

            var reversal = new AccountingPostRequest(
                book.Code,
                "reversal",
                request.Description.Trim(),
                request.BusinessDate,
                request.RequestToken.Trim(),
                request.RequestFingerprint.Trim(),
                request.Actor,
                originalLines.Select(line => new AccountingPostLine(
                    accounts[line.AccountId].Code,
                    line.CreditSatang,
                    line.DebitSatang,
                    new AccountingDimensions(
                        line.MemberId,
                        line.DeathCaseId,
                        line.BeneficiarySlotNo,
                        line.CollectionRequestId,
                        line.PartySnapshot,
                        line.DescriptionSnapshot))).ToList(),
                new AccountingSourceReference("reversal", original.Id.ToString(), original.JournalNumber),
                original.Id);
            var result = await PostNewAsync(reversal, linkedOperationId: null, cancellationToken);
            if (ownsTransaction)
                await transaction!.CommitAsync(cancellationToken);
            else if (savepointName is not null)
                await transaction!.ReleaseSavepointAsync(savepointName, cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                if (transaction is not null)
                {
                    if (ownsTransaction)
                        await transaction.RollbackAsync(cancellationToken);
                    else if (savepointName is not null)
                        await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                }
            }
            finally
            {
                trackerCheckpoint.Restore(database);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static void ValidateReverseRequest(AccountingReverseRequest request)
    {
        if (request.OriginalJournalId == Guid.Empty || string.IsNullOrWhiteSpace(request.RequestToken) ||
            string.IsNullOrWhiteSpace(request.RequestFingerprint) || string.IsNullOrWhiteSpace(request.Description))
            throw new AccountingValidationException("ต้องระบุรายการต้นฉบับ คำอธิบาย รหัสคำขอ และลายนิ้วมือคำขอของการยกเลิก");
        if (string.IsNullOrWhiteSpace(request.Actor.UserId) || string.IsNullOrWhiteSpace(request.Actor.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Actor.MachineName) || string.IsNullOrWhiteSpace(request.Actor.AppVersion))
            throw new AccountingValidationException("ต้องระบุผู้บันทึก เครื่อง และรุ่นโปรแกรมสำหรับการยกเลิกบัญชี");
    }

    private async Task<AccountingJournal?> FindExistingAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken) =>
        await (from journal in database.AccountingJournals
               join book in database.AccountingBooks on journal.BookId equals book.Id
               where book.Code == request.BookCode && journal.RequestToken == request.RequestToken.Trim()
               select journal)
            .SingleOrDefaultAsync(cancellationToken);

    private static void ValidateLinkedRequest(AccountingLinkedPostRequest request)
    {
        if (request.LinkedOperationId == Guid.Empty)
            throw new AccountingValidationException("ต้องระบุรหัสปฏิบัติการร่วมของทั้งสองสมุดบัญชี");
        if (request.WelfarePosting.BookCode != AccountingBookCode.Welfare ||
            request.AssociationPosting.BookCode != AccountingBookCode.Association)
            throw new AccountingValidationException("รายการร่วมต้องมีรายการบัญชีสวัสดิการและรายการบัญชีสมาคมอย่างละหนึ่งรายการ");
        ValidateRequest(request.WelfarePosting);
        ValidateRequest(request.AssociationPosting);
    }

    private static AccountingLinkedPostingResult ReplayLinkedPair(
        AccountingLinkedPostRequest request,
        AccountingJournal? welfareExisting,
        AccountingJournal? associationExisting)
    {
        if (welfareExisting is null || associationExisting is null ||
            welfareExisting.RequestFingerprint != request.WelfarePosting.RequestFingerprint.Trim() ||
            associationExisting.RequestFingerprint != request.AssociationPosting.RequestFingerprint.Trim() ||
            welfareExisting.LinkedOperationId != request.LinkedOperationId ||
            associationExisting.LinkedOperationId != request.LinkedOperationId)
            throw new AccountingIdempotencyConflictException("รหัสคำขอรายการร่วมนี้ถูกใช้กับข้อมูลบัญชีที่ต่างกันแล้ว");

        return new AccountingLinkedPostingResult(
            new AccountingPostingResult(welfareExisting.Id, welfareExisting.JournalNumber, true),
            new AccountingPostingResult(associationExisting.Id, associationExisting.JournalNumber, true),
            true);
    }

    private sealed class ChangeTrackerCheckpoint
    {
        private readonly Dictionary<object, TrackedEntity> entries;

        private ChangeTrackerCheckpoint(Dictionary<object, TrackedEntity> entries)
        {
            this.entries = entries;
        }

        public static ChangeTrackerCheckpoint Capture(AppDbContext database)
        {
            var entries = new Dictionary<object, TrackedEntity>(ReferenceEqualityComparer.Instance);
            foreach (var entry in database.ChangeTracker.Entries())
                entries.Add(entry.Entity, TrackedEntity.Capture(entry));

            return new ChangeTrackerCheckpoint(entries);
        }

        public void Restore(AppDbContext database)
        {
            foreach (var entry in database.ChangeTracker.Entries().ToArray())
            {
                if (entries.TryGetValue(entry.Entity, out var snapshot))
                {
                    snapshot.Restore(entry);
                    continue;
                }

                if (IsAccountingEntity(entry.Entity))
                    entry.State = EntityState.Detached;
            }
        }

        private static bool IsAccountingEntity(object entity) =>
            entity is AccountingBook or AccountingAccount or AccountingPeriod or AccountingJournal or
                AccountingJournalLine or AccountingPostingAudit;

        private sealed class TrackedEntity
        {
            private readonly EntityState state;
            private readonly IReadOnlyList<TrackedProperty> properties;

            private TrackedEntity(EntityState state, IReadOnlyList<TrackedProperty> properties)
            {
                this.state = state;
                this.properties = properties;
            }

            public static TrackedEntity Capture(EntityEntry entry) => new(
                entry.State,
                entry.Properties.Select(property => new TrackedProperty(
                    property.Metadata.Name,
                    property.CurrentValue,
                    property.OriginalValue,
                    property.IsModified)).ToArray());

            public void Restore(EntityEntry entry)
            {
                foreach (var property in properties)
                {
                    var trackedProperty = entry.Property(property.Name);
                    trackedProperty.CurrentValue = property.CurrentValue;
                    trackedProperty.OriginalValue = property.OriginalValue;
                }

                entry.State = state;
                foreach (var property in properties)
                    entry.Property(property.Name).IsModified = property.IsModified;
            }
        }

        private sealed record TrackedProperty(
            string Name,
            object? CurrentValue,
            object? OriginalValue,
            bool IsModified);
    }
}
