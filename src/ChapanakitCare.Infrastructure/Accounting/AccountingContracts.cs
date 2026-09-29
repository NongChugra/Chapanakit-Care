using ChapanakitCare.Domain.Entities;

namespace ChapanakitCare.Infrastructure.Accounting;

public sealed record AccountingActor(
    string UserId,
    string DisplayName,
    string MachineName,
    string AppVersion);

public sealed record AccountingSourceReference(
    string SourceType,
    string SourceId,
    string? SourceSnapshot = null);

public sealed record AccountingDimensions(
    Guid? MemberId = null,
    Guid? DeathCaseId = null,
    int? BeneficiarySlotNo = null,
    Guid? CollectionRequestId = null,
    string? PartySnapshot = null,
    string? DescriptionSnapshot = null);

public sealed record AccountingPostLine(
    string AccountCode,
    long DebitSatang,
    long CreditSatang,
    AccountingDimensions? Dimensions = null);

public sealed record AccountingPostRequest(
    AccountingBookCode BookCode,
    string VoucherType,
    string Description,
    DateOnly BusinessDate,
    string RequestToken,
    string RequestFingerprint,
    AccountingActor Actor,
    IReadOnlyList<AccountingPostLine> Lines,
    AccountingSourceReference? Source = null,
    Guid? ReversesJournalId = null);

public sealed record AccountingLinkedPostRequest(
    Guid LinkedOperationId,
    AccountingPostRequest WelfarePosting,
    AccountingPostRequest AssociationPosting);

public sealed record AccountingReverseRequest(
    Guid OriginalJournalId,
    DateOnly BusinessDate,
    string RequestToken,
    string RequestFingerprint,
    AccountingActor Actor,
    string Description);

public sealed record AccountingPostingResult(
    Guid JournalId,
    string JournalNumber,
    bool WasReplayed);

public sealed record AccountingLinkedPostingResult(
    AccountingPostingResult WelfarePosting,
    AccountingPostingResult AssociationPosting,
    bool WasReplayed);

public sealed record AccountingBookActivation(
    AccountingBookCode BookCode,
    DateOnly CutoverStartDate,
    string OpeningEvidence,
    AccountingActor Actor);

public sealed record AccountingBankAccountSetup(
    AccountingBookCode BookCode,
    string AccountCode,
    string AccountName,
    string BankDisplayName,
    AccountingActor Actor);

public sealed record AccountingCashAccountSetup(AccountingBookCode BookCode, string AccountCode, string AccountName, AccountingActor Actor);

public sealed record AccountingExpenseCategorySetup(
    string AccountCode,
    string AccountName,
    AccountingActor Actor);

public sealed record AccountingPeriodCommand(
    AccountingBookCode BookCode,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string Evidence,
    AccountingActor Actor);

public interface IAccountingPostingService
{
    Task<AccountingPostingResult> PostAsync(
        AccountingPostRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountingLinkedPostingResult> PostLinkedAsync(
        AccountingLinkedPostRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountingPostingResult> ReverseAsync(
        AccountingReverseRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AccountingValidationException(string message) : Exception(message);

public sealed class AccountingIdempotencyConflictException(string message) : Exception(message);

public sealed class AccountingPeriodClosedException(string message) : Exception(message);
