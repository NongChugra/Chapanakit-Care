namespace ChapanakitCare.Domain.Entities;

public enum MemberStatus
{
    Normal,
    Deceased,
    Resigned
}

public sealed class Member
{
    public Guid Id { get; set; }
    public string RunNo { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? PersonalIdCard { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? HouseNo { get; set; }
    public string? Under { get; set; }
    public string? Moo { get; set; }
    public string? Subdistrict { get; set; }
    public string District { get; set; } = "ร้องกวาง";
    public string Province { get; set; } = "แพร่";
    public string? PostalCode { get; set; }
    public string? Mobile { get; set; }
    public string? GroupNo { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public DateOnly ApprovalDate { get; set; }
    public DateOnly CoverageStartDate { get; set; }
    public MemberStatus Status { get; set; } = MemberStatus.Normal;
    public int AdvanceUnitsBalance { get; set; } = 30;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public string? ArchivedBy { get; set; }
    public string? ArchiveReason { get; set; }
}

public sealed class MemberBeneficiary
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public int SlotNo { get; set; }
    public string? Title { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? PersonalIdCard { get; set; }
    public string? Mobile { get; set; }
    public string? HouseNo { get; set; }
    public string? Under { get; set; }
    public string? Moo { get; set; }
    public string? Subdistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public string? ArchivedBy { get; set; }
    public string? ArchiveReason { get; set; }
}

public sealed class MemberStatusEvent
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class SystemSettings
{
    public int Id { get; set; } = 1;
    public int SettingsRevision { get; set; } = 1;
    public long? RegistrationFeeSatang { get; set; }
    public int ServiceFeeBasisPoints { get; set; } = 400;
    public long WelfarePerMemberSatang { get; set; } = 900;
    public int ResetTargetUnits { get; set; } = 30;
    public int CoverageWaitDays { get; set; } = 180;
    public int SpecialNonPayWindowDays { get; set; } = 365;
    public int DeathWarningThreshold { get; set; } = 25;
    public string ServiceFeeRoundingMode { get; set; } = "round_down_to_baht";
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class NumberSequence
{
    public string SequenceKey { get; set; } = string.Empty;
    public long NextValue { get; set; }
    public int Width { get; set; }
    public string? Prefix { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class DeathCase
{
    public Guid Id { get; set; }
    public string DeathCaseNo { get; set; } = string.Empty;
    public long DeathSequenceNo { get; set; }
    public Guid MemberId { get; set; }
    public DateOnly RecordedBusinessDate { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string DeathCertificateNo { get; set; } = string.Empty;
    public DateOnly DeathCertificateDate { get; set; }
    public string DeathCertificateFileName { get; set; } = string.Empty;
    public string DeathCertificateContentType { get; set; } = "application/pdf";
    public byte[] DeathCertificatePdf { get; set; } = [];
    public long DeathCertificateSize { get; set; }
    public string DeathCertificateSha256 { get; set; } = string.Empty;
    public string CauseOfDeathText { get; set; } = string.Empty;
    public bool IsManualNonPayCase { get; set; }
    public string? ManualNonPayReason { get; set; }
    public string EligibilityResult { get; set; } = string.Empty;
    public int DaysSinceCoverage { get; set; }
    public int SettingsRevision { get; set; }
    public string RecordState { get; set; } = "confirmed";
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}

public sealed class DeathMemberSnapshot
{
    public Guid DeathCaseId { get; set; }
    public string RunNo { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? PersonalIdCard { get; set; }
    public DateOnly? BirthDate { get; set; }
    public int? AgeAtDeath { get; set; }
    public string? HouseNo { get; set; }
    public string? Under { get; set; }
    public string? Moo { get; set; }
    public string? Subdistrict { get; set; }
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Mobile { get; set; }
    public string? GroupNo { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public DateOnly ApprovalDate { get; set; }
    public DateOnly CoverageStartDate { get; set; }
    public int AdvanceUnitsBeforeDeath { get; set; }
}

public sealed class DeathBeneficiarySnapshot
{
    public Guid Id { get; set; }
    public Guid DeathCaseId { get; set; }
    public int SlotNo { get; set; }
    public string? Title { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? PersonalIdCard { get; set; }
    public string? Mobile { get; set; }
    public string? HouseNo { get; set; }
    public string? Moo { get; set; }
    public string? Subdistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public int ShareNumerator { get; set; } = 1;
    public int ShareDenominator { get; set; }
}

public sealed class DeathCalculation
{
    public Guid DeathCaseId { get; set; }
    public bool IsPayable { get; set; }
    public int ContributorCount { get; set; }
    public long WelfarePerMemberSatang { get; set; }
    public long GrossCollectionSatang { get; set; }
    public int ServiceFeeBasisPoints { get; set; }
    public string ServiceFeeRoundingMode { get; set; } = string.Empty;
    public long ServiceFeeSatang { get; set; }
    public long NetCollectionSatang { get; set; }
    public int DeceasedAdvanceUnits { get; set; }
    public long DeceasedAdvanceValueSatang { get; set; }
    public long TotalBenefitSatang { get; set; }
    public int BeneficiaryCount { get; set; }
    public DateTimeOffset CalculatedAtUtc { get; set; }
}

public sealed class AdvanceLedgerEntry
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public long EntryOrder { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public int UnitsDelta { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public Guid? SourceDeathCaseId { get; set; }
    public Guid? SourceResetBatchId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class AdvanceResetBatch
{
    public Guid Id { get; set; }
    public string ResetNo { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public int TargetUnits { get; set; }
    public int DeathsSincePreviousReset { get; set; }
    public Guid? PreviousResetId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
}

public sealed class AdvanceResetLine
{
    public Guid Id { get; set; }
    public Guid ResetBatchId { get; set; }
    public Guid MemberId { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public int UnitsDelta { get; set; }
    public Guid LedgerEntryId { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string CycleKey { get; set; } = string.Empty;
    public DateOnly TriggeredBusinessDate { get; set; }
    public DateTimeOffset TriggeredAtUtc { get; set; }
    public int? DeathsSinceLatestReset { get; set; }
    public Guid? LatestResetId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string State { get; set; } = "active";
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public sealed class UiTablePreference
{
    public Guid Id { get; set; }
    public string ProfileKey { get; set; } = string.Empty;
    public string TableKey { get; set; } = string.Empty;
    public string ColumnOrderJson { get; set; } = "[]";
    public string HiddenColumnsJson { get; set; } = "[]";
    public string VisibleComponentsJson { get; set; } = "[]";
    public string? ColumnWidthsJson { get; set; }
    public string? SortJson { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ThaiAddressReference
{
    public int Id { get; set; }
    public string Subdistrict { get; set; } = string.Empty;
    public string District { get; set; } = "ร้องกวาง";
    public string Province { get; set; } = "แพร่";
    public string PostalCode { get; set; } = string.Empty;
    public string NormalizedSearch { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string? SourceVersion { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public Guid? MemberId { get; set; }
    public string? Reason { get; set; }
    public string AppVersion { get; set; } = string.Empty;
}

public sealed class AuditFieldChange
{
    public Guid Id { get; set; }
    public Guid AuditEventId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValueDisplay { get; set; }
    public string? NewValueDisplay { get; set; }
    public bool IsSensitive { get; set; }
}

public sealed class BackupRun
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public bool IsAutomatic { get; set; }
}
