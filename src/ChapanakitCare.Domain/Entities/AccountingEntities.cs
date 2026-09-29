namespace ChapanakitCare.Domain.Entities;

public enum AccountingBookCode
{
    Welfare,
    Association
}

public enum AccountingAccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense
}

public enum AccountingAccountRole
{
    Cash,
    Bank,
    MemberContributionShortfallReceivable,
    DueFromWelfare,
    MemberAdvanceLiability,
    WelfareBenefitPayable,
    FeeDueToAssociation,
    OpeningAccumulatedFund,
    WelfareDeductionIncome,
    OperatingExpense
}

public enum AccountingNormalBalance
{
    Debit,
    Credit
}

public enum AccountingPeriodStatus
{
    Open,
    Closed
}

public sealed class AccountingBook
{
    public Guid Id { get; set; }
    public AccountingBookCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JournalPrefix { get; set; } = string.Empty;
    public bool IsActivated { get; set; }
    public DateOnly? CutoverStartDate { get; set; }
    public string? OpeningEvidence { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public string? ActivatedBy { get; set; }
    public long NextJournalNumber { get; set; } = 1;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class AccountingAccount
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountingAccountType AccountType { get; set; }
    public AccountingAccountRole Role { get; set; }
    public AccountingNormalBalance NormalBalance { get; set; }
    public bool IsBankAccount { get; set; }
    public string? BankDisplayName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class AccountingPeriod
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }
    public string? ClosureEvidence { get; set; }
    public DateTimeOffset? ReopenedAtUtc { get; set; }
    public string? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class AccountingJournal
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? LinkedOperationId { get; set; }
    public string JournalNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string RequestToken { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public Guid? ReversesJournalId { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? SourceSnapshot { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
}

public sealed class AccountingJournalLine
{
    public Guid Id { get; set; }
    public Guid JournalId { get; set; }
    public Guid BookId { get; set; }
    public Guid AccountId { get; set; }
    public int LineNo { get; set; }
    public long DebitSatang { get; set; }
    public long CreditSatang { get; set; }
    public Guid? MemberId { get; set; }
    public Guid? DeathCaseId { get; set; }
    public int? BeneficiarySlotNo { get; set; }
    public Guid? CollectionRequestId { get; set; }
    public string? PartySnapshot { get; set; }
    public string? DescriptionSnapshot { get; set; }
}

public sealed class AccountingPostingAudit
{
    public Guid Id { get; set; }
    public Guid JournalId { get; set; }
    public Guid OperationId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string DetailsSnapshot { get; set; } = string.Empty;
}
