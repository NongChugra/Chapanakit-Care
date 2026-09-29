namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed record MemberReceipt(Guid MemberId, long AmountSatang, Guid? CollectionId = null);
public sealed record ReceiveWelfareMoney(DateOnly Date, string MoneyAccount, string Description,
    string RequestToken, IReadOnlyList<MemberReceipt> Members);
public sealed record PayWelfareBenefit(Guid DeathCaseId, int BeneficiarySlot, DateOnly Date,
    string MoneyAccount, long AmountSatang, string Evidence, string RequestToken);
public sealed record PayOperatingExpense(DateOnly Date, string MoneyAccount, string ExpenseAccount,
    long AmountSatang, string Payee, string Evidence, string RequestToken);
public sealed record TransferBookMoney(string Book, DateOnly Date, string FromAccount,
    string ToAccount, long AmountSatang, string Evidence, string RequestToken);
public sealed record RemitAssociationFee(DateOnly Date, string WelfareMoneyAccount,
    string AssociationMoneyAccount, long AmountSatang, string Evidence, string RequestToken);
public sealed record ReverseFinanceDocument(Guid JournalId, DateOnly Date, string Reason, string RequestToken);
public sealed record OpeningAccountAmount(string AccountCode, long DebitSatang, long CreditSatang,
    Guid? MemberId = null, Guid? DeathCaseId = null, int? BeneficiarySlot = null);
public sealed record OpenFinanceBook(string Book, DateOnly Date, string Evidence,
    string RequestToken, IReadOnlyList<OpeningAccountAmount> Amounts);
public sealed record ReconcileMoneyAccount(string Book, string AccountCode, DateOnly Date,
    long StatementBalanceSatang, string Evidence, string RequestToken);
public sealed record MoneyReconciliation(Guid Id, string Book, string AccountCode, DateOnly Date,
    long BookBalanceSatang, long StatementBalanceSatang, long DifferenceSatang, string Evidence, string RequestFingerprint);
public sealed record RefundMemberAdvance(Guid MemberId, DateOnly Date, string MoneyAccount,
    long AmountSatang, string Evidence, string RequestToken);
public sealed record ReclassifyExpense(Guid OriginalJournalId, DateOnly Date, string FromExpenseAccount,
    string ToExpenseAccount, long AmountSatang, string Evidence, string RequestToken);
