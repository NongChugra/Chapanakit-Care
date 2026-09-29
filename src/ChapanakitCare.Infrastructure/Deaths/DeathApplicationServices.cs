using System.Globalization;
using System.Security.Cryptography;
using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.AccountingOperations;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Deaths;

public sealed record DeathCertificateDocument(string FileName, string ContentType, byte[] Bytes);
public sealed record StoredDeathCertificate(string FileName, string ContentType, byte[] Bytes);
public sealed record ConfirmDeathCommand(
    string RunNo,
    string DeathCertificateNo,
    DateOnly DeathCertificateDate,
    string CauseOfDeath,
    bool IsManualNonPayCase,
    string? ManualNonPayReason,
    DeathCertificateDocument CertificateDocument,
    IReadOnlyList<RecipientPhotoDocument>? RecipientPhotos = null,
    DateOnly? ReportedCertificateDate = null);
public sealed record ConfirmDeathResult(DeathCase DeathCase, DeathCalculation Calculation);
public sealed record DeathPreview(
    Member Member,
    IReadOnlyList<MemberBeneficiary> Beneficiaries,
    DeathBenefitResult Calculation,
    IReadOnlyList<long> BeneficiarySharesSatang,
    int DaysSinceCoverage,
    int SpecialNonPayWindowDays,
    bool IsWithinSpecialNonPayWindow,
    int ServiceFeeBasisPoints);

public sealed class DeathApplicationService(AppDbContext database)
{
    private const int MaximumCertificateBytes = 10 * 1024 * 1024;

    public async Task<string> GetNextDeathCaseNoAsync(CancellationToken ct = default)
    {
        var sequence = await database.NumberSequences.AsNoTracking()
            .SingleOrDefaultAsync(value => value.SequenceKey == "death_case_no", ct);
        var next = sequence?.NextValue ?? 1;
        var prefix = sequence?.Prefix ?? "D";
        var width = sequence?.Width ?? 5;
        return $"{prefix}{next.ToString($"D{width}", CultureInfo.InvariantCulture)}";
    }

    public async Task<StoredDeathCertificate> GetCertificateAsync(Guid deathCaseId, CancellationToken ct = default)
    {
        var certificate = await database.DeathCases.AsNoTracking()
            .Where(value => value.Id == deathCaseId && value.RecordState == "confirmed")
            .Select(value => new StoredDeathCertificate(
                value.DeathCertificateFileName,
                value.DeathCertificateContentType,
                value.DeathCertificatePdf))
            .SingleOrDefaultAsync(ct);
        return certificate ?? throw new MemberValidationException("ไม่พบไฟล์ใบมรณะบัตร");
    }

    public async Task<DeathPreview> PreviewAsync(
        string runNo,
        bool isManualNonPayCase,
        DateOnly businessDate,
        CancellationToken ct = default,
        DateOnly? deathCertificateDate = null)
    {
        var member = await database.Members.AsNoTracking().SingleOrDefaultAsync(
            value => value.RunNo == runNo.Trim() && value.ArchivedAtUtc == null,
            ct) ?? throw new MemberValidationException("ไม่พบเลขสมาชิก");
        if (member.Status == MemberStatus.Deceased)
        {
            throw new MemberValidationException("สมาชิกนี้ถูกบันทึกว่าเสียชีวิตแล้ว");
        }

        var beneficiaries = await database.MemberBeneficiaries.AsNoTracking()
            .Where(value => value.MemberId == member.Id && value.IsActive)
            .OrderBy(value => value.SlotNo)
            .ToListAsync(ct);
        if (beneficiaries.Count is < 1 or > 2)
        {
            throw new MemberValidationException("สมาชิกต้องมีผู้รับเงินสงเคราะห์ 1-2 คน");
        }

        var settings = await database.SystemSettings.AsNoTracking().SingleAsync(ct);
        var effectiveDeathDate = deathCertificateDate ?? businessDate;
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            effectiveDeathDate,
            member.CoverageStartDate,
            isManualNonPayCase,
            await database.Members.CountAsync(value => value.Status == MemberStatus.Normal && value.Id != member.Id && value.ArchivedAtUtc == null, ct),
            settings.WelfarePerMemberSatang,
            settings.ServiceFeeBasisPoints,
            member.AdvanceUnitsBalance,
            beneficiaries.Count,
            await new DeathAccountingService(database).GetAdvanceAsync(member.Id, ct)));
        var daysSinceCoverage = effectiveDeathDate.DayNumber - member.CoverageStartDate.DayNumber;
        return new DeathPreview(
            member,
            beneficiaries,
            result,
            result.BeneficiarySharesSatang,
            daysSinceCoverage,
            settings.SpecialNonPayWindowDays,
            SpecialNonPayEligibility.WithinWindow(effectiveDeathDate, member.CoverageStartDate, settings.SpecialNonPayWindowDays),
            settings.ServiceFeeBasisPoints);
    }

    public async Task<ConfirmDeathResult> ConfirmAsync(ConfirmDeathCommand command, DateOnly businessDate, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RunNo) || string.IsNullOrWhiteSpace(command.DeathCertificateNo) || string.IsNullOrWhiteSpace(command.CauseOfDeath) || command.IsManualNonPayCase && string.IsNullOrWhiteSpace(command.ManualNonPayReason))
            throw new MemberValidationException("กรุณากรอกข้อมูลการเสียชีวิตให้ครบ");
        var certificate = ValidateCertificate(command.CertificateDocument);
        var photos = (command.RecipientPhotos ?? []).Select(photo => photo.Validate()).ToArray();
        if (photos.Select(photo => photo.BeneficiarySlotNo).Distinct().Count() != photos.Length)
            throw new MemberValidationException("แนบภาพผู้รับเงินได้คนละ 1 ภาพ");

        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        ConfirmDeathResult confirmedResult;
        try
        {
        var member = await database.Members.SingleOrDefaultAsync(x => x.RunNo == command.RunNo.Trim() && x.ArchivedAtUtc == null, ct)
            ?? throw new MemberValidationException("ไม่พบเลขสมาชิก");
        if (member.Status == MemberStatus.Deceased) throw new MemberValidationException("สมาชิกนี้ถูกบันทึกว่าเสียชีวิตแล้ว");
        if (command.DeathCertificateDate < member.ApplicationDate || command.DeathCertificateDate > businessDate ||
            command.ReportedCertificateDate is { } reported && (reported < command.DeathCertificateDate || reported > businessDate))
            throw new MemberValidationException("กรุณาตรวจสอบปี พ.ศ.: วันเสียชีวิตต้องอยู่ตั้งแต่วันที่สมัครถึงวันนี้ และวันที่แจ้งต้องอยู่ตั้งแต่วันเสียชีวิตถึงวันนี้");
        var settings = await database.SystemSettings.AsNoTracking().SingleAsync(ct);
        var beneficiaries = await database.MemberBeneficiaries.Where(x => x.MemberId == member.Id && x.IsActive).OrderBy(x => x.SlotNo).ToListAsync(ct);
        if (command.IsManualNonPayCase && !SpecialNonPayEligibility.WithinWindow(
                command.DeathCertificateDate, member.CoverageStartDate, settings.SpecialNonPayWindowDays))
            throw new MemberValidationException("เลือกโรคร้ายแรงได้เฉพาะวันที่เสียชีวิตในช่วงเฝ้าระวังหลังเริ่มคุ้มครอง");
        if (beneficiaries.Count is < 1 or > 2) throw new MemberValidationException("สมาชิกต้องมีผู้รับเงินสงเคราะห์ 1-2 คน");
        if (photos.Any(photo => beneficiaries.All(b => b.SlotNo != photo.BeneficiarySlotNo)))
            throw new MemberValidationException("ไม่พบผู้รับเงินตามช่องภาพที่แนบ");
        var sequence = await GetSequence("death_case_no", "D", now, ct);
        var caseId = Guid.NewGuid();
        var caseNo = $"{sequence.Prefix}{sequence.NextValue.ToString($"D{sequence.Width}", CultureInfo.InvariantCulture)}";
        var calculationResult = DeathBenefitCalculator.Calculate(new DeathBenefitInput(command.DeathCertificateDate, member.CoverageStartDate, command.IsManualNonPayCase,
            await database.Members.CountAsync(x => x.Status == MemberStatus.Normal && x.Id != member.Id && x.ArchivedAtUtc == null, ct),
            settings.WelfarePerMemberSatang, settings.ServiceFeeBasisPoints, member.AdvanceUnitsBalance, beneficiaries.Count,
            await new DeathAccountingService(database).GetAdvanceAsync(member.Id, ct)));
        var death = new DeathCase { Id = caseId, DeathCaseNo = caseNo, DeathSequenceNo = sequence.NextValue++, MemberId = member.Id, RecordedBusinessDate = businessDate, RecordedAtUtc = now, DeathCertificateNo = command.DeathCertificateNo.Trim(), DeathCertificateDate = command.DeathCertificateDate, DeathCertificateFileName = certificate.FileName, DeathCertificateContentType = "application/pdf", DeathCertificatePdf = certificate.Bytes, DeathCertificateSize = certificate.Bytes.LongLength, DeathCertificateSha256 = Convert.ToHexString(SHA256.HashData(certificate.Bytes)).ToLowerInvariant(), CauseOfDeathText = command.CauseOfDeath.Trim(), IsManualNonPayCase = command.IsManualNonPayCase, ManualNonPayReason = Clean(command.ManualNonPayReason), EligibilityResult = Eligibility(calculationResult.Eligibility), DaysSinceCoverage = command.DeathCertificateDate.DayNumber - member.CoverageStartDate.DayNumber, SettingsRevision = settings.SettingsRevision, ConfirmedAtUtc = now, ConfirmedBy = actor };
        sequence.UpdatedAtUtc = now;
        database.DeathCases.Add(death);
        death.ReportedCertificateDate = command.ReportedCertificateDate;
        foreach (var photo in photos)
            database.DeathRecipientPhotos.Add(new DeathRecipientPhoto
            {
                Id = Guid.NewGuid(), DeathCaseId = caseId, BeneficiarySlotNo = photo.BeneficiarySlotNo,
                FileName = photo.FileName, ContentType = photo.ContentType, Bytes = photo.Bytes,
                Sha256 = Convert.ToHexString(SHA256.HashData(photo.Bytes)).ToLowerInvariant(), CreatedAtUtc = now, CreatedBy = actor
            });
        database.DeathMemberSnapshots.Add(Snapshot(death.Id, member, command.DeathCertificateDate));
        foreach (var b in beneficiaries) database.DeathBeneficiarySnapshots.Add(BeneficiarySnapshot(death.Id, b, beneficiaries.Count));
        var calculation = new DeathCalculation { DeathCaseId = caseId, IsPayable = calculationResult.Eligibility == DeathEligibility.Payable, ContributorCount = calculationResult.ContributorCount, WelfarePerMemberSatang = settings.WelfarePerMemberSatang, GrossCollectionSatang = calculationResult.GrossCollectionSatang, ServiceFeeBasisPoints = settings.ServiceFeeBasisPoints, ServiceFeeRoundingMode = settings.ServiceFeeRoundingMode, ServiceFeeSatang = calculationResult.ServiceFeeSatang, NetCollectionSatang = calculationResult.NetCollectionSatang, DeceasedAdvanceUnits = member.AdvanceUnitsBalance, DeceasedAdvanceValueSatang = calculationResult.DeceasedAdvanceValueSatang, TotalBenefitSatang = calculationResult.TotalBenefitSatang, BeneficiaryCount = beneficiaries.Count, CalculatedAtUtc = now };
        database.DeathCalculations.Add(calculation);
        await AddLedger(member, -member.AdvanceUnitsBalance, "correction", businessDate, caseId, null, "คืนเงินสงเคราะห์ล่วงหน้าและปิดยอดสมาชิกผู้เสียชีวิต", now, actor, ct);
        member.AdvanceUnitsBalance = 0; member.Status = MemberStatus.Deceased; member.Version++; member.UpdatedAtUtc = now; member.UpdatedBy = actor;
        if (calculationResult.ShouldDecrementContributors)
            foreach (var contributor in await database.Members.Where(x => x.Status == MemberStatus.Normal && x.Id != member.Id && x.ArchivedAtUtc == null).ToListAsync(ct))
            { await AddLedger(contributor, -1, "death_contribution", businessDate, caseId, null, $"เงินสงเคราะห์กรณี {caseNo}", now, actor, ct); contributor.AdvanceUnitsBalance--; contributor.Version++; contributor.UpdatedAtUtc = now; contributor.UpdatedBy = actor; }
        database.MemberStatusEvents.Add(new MemberStatusEvent { Id = Guid.NewGuid(), MemberId = member.Id, FromStatus = "normal", ToStatus = "deceased", EffectiveDate = businessDate, SourceType = "death_case", SourceId = caseId, CreatedAtUtc = now, CreatedBy = actor });
        var deathAudit = Audit("death.confirmed", "death_case", caseId.ToString(), member.Id, now, actor);
        deathAudit.Reason = $"{caseNo} · {member.RunNo} {member.FirstName} {member.LastName} · ภาพผู้รับเงิน {photos.Length} ภาพ";
        database.AuditEvents.Add(deathAudit);
        await ChapanakitCare.Infrastructure.Coordinators.CoordinatorApplicationService.EndForMemberAsync(database, member, "สิ้นสุดหน้าที่เนื่องจากสมาชิกเสียชีวิต", businessDate, now, actor, ct);
        await new DeathAccountingService(database).AccrueAsync(death, calculationResult, beneficiaries, settings.ServiceFeeBasisPoints, actor, ct);
        await database.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        confirmedResult = new(death, calculation);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            database.ChangeTracker.Clear();
            throw;
        }
        await new NotificationService(database).RefreshAsync(businessDate, now, ct);
        return confirmedResult;
    }

    private async Task<NumberSequence> GetSequence(string key, string prefix, DateTimeOffset now, CancellationToken ct)
    { var s = await database.NumberSequences.SingleOrDefaultAsync(x => x.SequenceKey == key, ct); if (s is not null) return s; s = new NumberSequence { SequenceKey = key, Prefix = prefix, NextValue = 1, Width = 5, UpdatedAtUtc = now }; database.NumberSequences.Add(s); return s; }
    private async Task AddLedger(Member m, int delta, string type, DateOnly date, Guid? deathId, Guid? resetId, string reason, DateTimeOffset now, string actor, CancellationToken ct)
    { var order = (await database.AdvanceLedgerEntries.Where(x => x.MemberId == m.Id).MaxAsync(x => (long?)x.EntryOrder, ct) ?? 0) + 1; database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry { Id = Guid.NewGuid(), MemberId = m.Id, EntryOrder = order, EntryType = type, BusinessDate = date, UnitsDelta = delta, BalanceBefore = m.AdvanceUnitsBalance, BalanceAfter = m.AdvanceUnitsBalance + delta, SourceDeathCaseId = type == "death_contribution" ? deathId : null, SourceResetBatchId = resetId, Reason = reason, CreatedAtUtc = now, CreatedBy = actor }); }
    private static string Eligibility(DeathEligibility e) => e switch { DeathEligibility.Payable => "payable", DeathEligibility.BeforeCoverageZero => "before_coverage_zero", _ => "manual_nonpay_zero" };
    private static DeathMemberSnapshot Snapshot(Guid id, Member m, DateOnly date) => new() { DeathCaseId = id, RunNo = m.RunNo, Title = m.Title, FirstName = m.FirstName, LastName = m.LastName, Gender = m.Gender, PersonalIdCard = m.PersonalIdCard, BirthDate = m.BirthDate, AgeAtDeath = m.BirthDate is null ? null : AgeCalculator.CompletedYears(m.BirthDate.Value, date), HouseNo = m.HouseNo, Under = m.Under, Moo = m.Moo, Subdistrict = m.Subdistrict, District = m.District, Province = m.Province, PostalCode = m.PostalCode, Mobile = m.Mobile, GroupNo = m.GroupNo, ApplicationDate = m.ApplicationDate, ApprovalDate = m.ApprovalDate, CoverageStartDate = m.CoverageStartDate, AdvanceUnitsBeforeDeath = m.AdvanceUnitsBalance };
    private static DeathBeneficiarySnapshot BeneficiarySnapshot(Guid id, MemberBeneficiary b, int count) => new() { Id = Guid.NewGuid(), DeathCaseId = id, SlotNo = b.SlotNo, Title = b.Title, FirstName = b.FirstName, LastName = b.LastName, Relationship = b.Relationship, PersonalIdCard = b.PersonalIdCard, Mobile = b.Mobile, HouseNo = b.HouseNo, Moo = b.Moo, Subdistrict = b.Subdistrict, District = b.District, Province = b.Province, PostalCode = b.PostalCode, ShareDenominator = count };
    private static AuditEvent Audit(string action, string type, string id, Guid? member, DateTimeOffset now, string actor) => new() { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), OccurredAtUtc = now, ActorUserId = "local-user", ActorDisplayName = actor, MachineName = Environment.MachineName, Action = action, EntityType = type, EntityId = id, MemberId = member, AppVersion = "checkpoint-3" };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DeathCertificateDocument ValidateCertificate(DeathCertificateDocument? document)
    {
        if (document is null || document.Bytes.Length == 0)
            throw new MemberValidationException("กรุณาแนบใบมรณะบัตร PDF");
        if (document.Bytes.Length > MaximumCertificateBytes)
            throw new MemberValidationException("ไฟล์ใบมรณะบัตร PDF ต้องมีขนาดไม่เกิน 10 MB");
        if (document.Bytes.Length < 5 || !document.Bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new MemberValidationException("ไฟล์ใบมรณะบัตรต้องเป็น PDF ที่ถูกต้อง");
        var fileName = Path.GetFileName(document.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "death-certificate.pdf";
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            fileName += ".pdf";
        return document with { FileName = fileName, ContentType = "application/pdf", Bytes = document.Bytes.ToArray() };
    }
}

public sealed class AdvanceResetService(AppDbContext database)
{
    public async Task<AdvanceResetBatch> ResetAsync(string trigger, string idempotencyKey, DateOnly date, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        var existing = await database.AdvanceResetBatches.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct); if (existing is not null) return existing;
        if (await database.AccountingBooks.AnyAsync(x => x.Code == AccountingBookCode.Welfare && x.IsActivated, ct))
            throw new MemberValidationException("เปิดบัญชีเงินสงเคราะห์แล้ว กรุณาสร้างใบเรียกเก็บและบันทึกรับเงินจริงแทนการรีเซ็ตยอด");
        await using var tx = await database.Database.BeginTransactionAsync(ct); var settings = await database.SystemSettings.AsNoTracking().SingleAsync(ct);
        var previous = (await database.AdvanceResetBatches.AsNoTracking().ToListAsync(ct)).OrderByDescending(x => x.ConfirmedAtUtc).FirstOrDefault();
        var sequence = await database.NumberSequences.SingleOrDefaultAsync(x => x.SequenceKey == "reset_no", ct) ?? new NumberSequence { SequenceKey = "reset_no", Prefix = "R", NextValue = 1, Width = 5, UpdatedAtUtc = now }; if (database.Entry(sequence).State == EntityState.Detached) database.NumberSequences.Add(sequence);
        var batch = new AdvanceResetBatch { Id = Guid.NewGuid(), ResetNo = $"{sequence.Prefix}{sequence.NextValue.ToString($"D{sequence.Width}", CultureInfo.InvariantCulture)}", TriggerType = trigger, TargetUnits = settings.ResetTargetUnits, DeathsSincePreviousReset = await database.DeathCases.CountAsync(x => x.RecordState == "confirmed" && (previous == null || x.ConfirmedAtUtc > previous.ConfirmedAtUtc), ct), PreviousResetId = previous?.Id, IdempotencyKey = idempotencyKey, ConfirmedAtUtc = now, ConfirmedBy = actor }; sequence.NextValue++; sequence.UpdatedAtUtc = now; database.AdvanceResetBatches.Add(batch);
        foreach (var m in await database.Members.Where(x => x.Status == MemberStatus.Normal && x.ArchivedAtUtc == null).ToListAsync(ct)) { var before = m.AdvanceUnitsBalance; var ledger = new AdvanceLedgerEntry { Id = Guid.NewGuid(), MemberId = m.Id, EntryOrder = (await database.AdvanceLedgerEntries.Where(x => x.MemberId == m.Id).MaxAsync(x => (long?)x.EntryOrder, ct) ?? 0) + 1, EntryType = "reset_to_30", BusinessDate = date, UnitsDelta = settings.ResetTargetUnits - before, BalanceBefore = before, BalanceAfter = settings.ResetTargetUnits, SourceResetBatchId = batch.Id, Reason = "รีเซ็ตยอดเงินสงเคราะห์ล่วงหน้า", CreatedAtUtc = now, CreatedBy = actor }; database.AdvanceLedgerEntries.Add(ledger); database.AdvanceResetLines.Add(new AdvanceResetLine { Id = Guid.NewGuid(), ResetBatchId = batch.Id, MemberId = m.Id, BalanceBefore = before, BalanceAfter = settings.ResetTargetUnits, UnitsDelta = settings.ResetTargetUnits - before, LedgerEntryId = ledger.Id }); m.AdvanceUnitsBalance = settings.ResetTargetUnits; m.Version++; m.UpdatedAtUtc = now; m.UpdatedBy = actor; }
        foreach (var notice in await database.Notifications.Where(x => x.State == "active").ToListAsync(ct))
        {
            notice.State = "acknowledged";
            notice.AcknowledgedAtUtc = now;
            notice.AcknowledgedBy = actor;
        }
        database.AuditEvents.Add(ActivityAudit.Create("advance.reset", "advance_reset", batch.Id.ToString(), now, actor,
            $"{batch.ResetNo} · ปรับยอดสมาชิกปกติเป็น {batch.TargetUnits} คน · เหตุผล {trigger}"));
        await database.SaveChangesAsync(ct); await tx.CommitAsync(ct); return batch;
    }
}

public sealed class NotificationService(AppDbContext database)
{
    public async Task RefreshAsync(DateOnly date, DateTimeOffset now, CancellationToken ct = default)
    {
        var accountingActive = await database.AccountingBooks.AnyAsync(x => x.Code == AccountingBookCode.Welfare && x.IsActivated, ct);
        if (accountingActive)
        {
            var obsolete = await database.Notifications.Where(x => x.State == "active" && !x.CycleKey.StartsWith("accounting:")).ToListAsync(ct);
            foreach (var notice in obsolete)
            {
                notice.State = "acknowledged";
                notice.AcknowledgedAtUtc = now;
                notice.AcknowledgedBy = "system_accounting_activation";
            }
            var month = "accounting:" + date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (date.Day == 1)
                await Add("month_start_reset", month, "ต้นเดือน กรุณาตรวจยอดเงินสมาชิกและจัดรอบเรียกเก็บตามความจำเป็น บันทึกรับเงินเมื่อได้รับเงินจริง", null, null, date, now, ct);
            var count = await database.DeathCases.CountAsync(x => x.RecordState == "confirmed", ct);
            var warning = await database.SystemSettings.Select(x => x.DeathWarningThreshold).SingleAsync(ct);
            if (count > warning)
                await Add("death_threshold", month, $"มีผู้เสียชีวิตสะสม {count} ราย กรุณาตรวจยอดค้างชำระและรอบเรียกเก็บสมาชิก", count, null, date, now, ct);
            await database.SaveChangesAsync(ct);
            return;
        }
        var latest = (await database.AdvanceResetBatches.AsNoTracking().ToListAsync(ct)).OrderByDescending(x => x.ConfirmedAtUtc).FirstOrDefault();
        var cycle = latest?.Id.ToString() ?? "initial";
        var confirmedDeathTimes = await database.DeathCases
            .Where(x => x.RecordState == "confirmed")
            .Select(x => x.ConfirmedAtUtc)
            .ToListAsync(ct);
        var deaths = latest is null
            ? confirmedDeathTimes.Count
            : confirmedDeathTimes.Count(value => value > latest.ConfirmedAtUtc);
        var threshold = (await database.SystemSettings.AsNoTracking().SingleAsync(ct)).DeathWarningThreshold;
        if (date.Day == 1) await Add("month_start_reset", date.ToString("yyyy-MM", CultureInfo.InvariantCulture), "ถึงวันที่ 1 ของเดือน กรุณาพิจารณารีเซ็ตยอดล่วงหน้า", null, latest?.Id, date, now, ct);
        if (deaths > threshold) await Add("death_threshold", cycle, $"มีผู้เสียชีวิต {deaths} รายตั้งแต่รีเซ็ตครั้งล่าสุด กรุณาพิจารณารีเซ็ตยอดล่วงหน้า", deaths, latest?.Id, date, now, ct);
        await database.SaveChangesAsync(ct);
    }
    private async Task Add(string type, string cycle, string message, int? deaths, Guid? reset, DateOnly date, DateTimeOffset now, CancellationToken ct) { if (await database.Notifications.AnyAsync(x => x.NotificationType == type && x.CycleKey == cycle, ct)) return; database.Notifications.Add(new Notification { Id = Guid.NewGuid(), NotificationType = type, CycleKey = cycle, TriggeredBusinessDate = date, TriggeredAtUtc = now, DeathsSinceLatestReset = deaths, LatestResetId = reset, Message = message }); }
}
