using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed partial class FinanceOperationService
{
    public async Task<Guid> PayBenefitAsync(PayWelfareBenefit command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        if (command.AmountSatang <= 0 || command.BeneficiarySlot is not (1 or 2))
            throw new AccountingValidationException("กรุณาระบุจำนวนเงินบวกและผู้รับเงินที่ถูกต้อง");
        var fingerprint = Fingerprint("benefit_payment", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(AccountingBookCode.Welfare, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var accounts = await BookAccountsAsync(AccountingBookCode.Welfare, ct);
        var cash = MoneyAccount(accounts, command.MoneyAccount);
        var recipient = await database.DeathBeneficiarySnapshots.AsNoTracking().SingleOrDefaultAsync(x =>
            x.DeathCaseId == command.DeathCaseId && x.SlotNo == command.BeneficiarySlot, ct)
            ?? throw new AccountingValidationException("ไม่พบผู้รับเงินของเหตุเสียชีวิตนี้");
        var payable = accounts["2100"];
        var lines = await database.AccountingJournalLines.Where(x => x.AccountId == payable.Id
            && x.DeathCaseId == command.DeathCaseId && x.BeneficiarySlotNo == command.BeneficiarySlot).ToListAsync(ct);
        if (Sum(lines.Select(x => checked(x.CreditSatang - x.DebitSatang))) < command.AmountSatang)
            throw new AccountingValidationException("จำนวนเงินเกินยอดค้างจ่ายของผู้รับเงินรายนี้");
        await RequireCashAsync(cash, command.AmountSatang, ct);
        var dimensions = new AccountingDimensions(DeathCaseId: command.DeathCaseId, BeneficiarySlotNo: command.BeneficiarySlot,
            PartySnapshot: $"{recipient.Title}{recipient.FirstName} {recipient.LastName}", DescriptionSnapshot: command.Evidence);
        var posted = await new AccountingPostingService(database).PostAsync(new(AccountingBookCode.Welfare, "benefit_payment",
            command.Evidence, command.Date, command.RequestToken, fingerprint, Actor(actor),
            [new("2100", command.AmountSatang, 0, dimensions), new(command.MoneyAccount, 0, command.AmountSatang)],
            new("benefit_payment", command.RequestToken, JsonSerializer.Serialize(command))), ct);
        await tx.CommitAsync(ct);
        return posted.JournalId;
    }

    public async Task<Guid> RemitFeeAsync(RemitAssociationFee command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        if (command.AmountSatang <= 0) throw new AccountingValidationException("จำนวนเงินต้องมากกว่าศูนย์");
        var fingerprint = Fingerprint("fee_remittance", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(AccountingBookCode.Welfare, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var welfare = await BookAccountsAsync(AccountingBookCode.Welfare, ct);
        var association = await BookAccountsAsync(AccountingBookCode.Association, ct);
        var cash = MoneyAccount(welfare, command.WelfareMoneyAccount);
        MoneyAccount(association, command.AssociationMoneyAccount);
        var dueTo = welfare["2200"];
        var dueFrom = association["1300"];
        var dueLines = await database.AccountingJournalLines.Where(x => x.AccountId == dueTo.Id || x.AccountId == dueFrom.Id).ToListAsync(ct);
        var payable = Sum(dueLines.Where(x => x.AccountId == dueTo.Id).Select(x => checked(x.CreditSatang - x.DebitSatang)));
        var receivable = Sum(dueLines.Where(x => x.AccountId == dueFrom.Id).Select(x => checked(x.DebitSatang - x.CreditSatang)));
        if (payable != receivable || command.AmountSatang > payable)
            throw new AccountingValidationException("ยอดค้างนำส่งและค้างรับต้องตรงกัน และจำนวนเงินต้องไม่เกินยอดค้าง");
        await RequireCashAsync(cash, command.AmountSatang, ct);
        var source = new AccountingSourceReference("fee_remittance", command.RequestToken, JsonSerializer.Serialize(command));
        var posted = await new AccountingPostingService(database).PostLinkedAsync(new(Guid.NewGuid(),
            new(AccountingBookCode.Welfare, "fee_remittance", command.Evidence, command.Date, command.RequestToken, fingerprint, Actor(actor),
                [new("2200", command.AmountSatang, 0), new(command.WelfareMoneyAccount, 0, command.AmountSatang)], source),
            new(AccountingBookCode.Association, "fee_remittance", command.Evidence, command.Date, command.RequestToken, fingerprint, Actor(actor),
                [new(command.AssociationMoneyAccount, command.AmountSatang, 0), new("1300", 0, command.AmountSatang)], source)), ct);
        await tx.CommitAsync(ct);
        return posted.WelfarePosting.JournalId;
    }

    public async Task<Guid> PayExpenseAsync(PayOperatingExpense command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        if (command.AmountSatang <= 0 || string.IsNullOrWhiteSpace(command.Payee) || command.Payee.Length > 300)
            throw new AccountingValidationException("กรุณาระบุผู้รับเงินและจำนวนเงินบวก");
        var fingerprint = Fingerprint("expense", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(AccountingBookCode.Association, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var accounts = await BookAccountsAsync(AccountingBookCode.Association, ct);
        var cash = MoneyAccount(accounts, command.MoneyAccount);
        if (!accounts.TryGetValue(command.ExpenseAccount, out var expense) || expense.Role != AccountingAccountRole.OperatingExpense)
            throw new AccountingValidationException("กรุณาเลือกหมวดค่าใช้จ่ายดำเนินงานของสมาคม");
        await RequireCashAsync(cash, command.AmountSatang, ct);
        var dimensions = new AccountingDimensions(PartySnapshot: command.Payee, DescriptionSnapshot: command.Evidence);
        var posted = await new AccountingPostingService(database).PostAsync(new(AccountingBookCode.Association, "operating_expense",
            command.Evidence, command.Date, command.RequestToken, fingerprint, Actor(actor),
            [new(command.ExpenseAccount, command.AmountSatang, 0, dimensions), new(command.MoneyAccount, 0, command.AmountSatang, dimensions)],
            new("operating_expense", command.RequestToken, JsonSerializer.Serialize(command))), ct);
        await tx.CommitAsync(ct);
        return posted.JournalId;
    }

    public async Task<Guid> TransferAsync(TransferBookMoney command, DateTimeOffset now, string actor, CancellationToken ct = default)
    {
        var code = ParseBook(command.Book);
        ValidateEnvelope(command.Date, command.Evidence, command.RequestToken, actor);
        if (command.AmountSatang <= 0 || command.FromAccount == command.ToAccount)
            throw new AccountingValidationException("จำนวนเงินต้องเป็นบวกและบัญชีต้นทางปลายทางต้องต่างกัน");
        var fingerprint = Fingerprint("transfer", command);
        await using var tx = await database.Database.BeginTransactionAsync(ct);
        var replay = await ReplayAsync(code, command.RequestToken, fingerprint, ct);
        if (replay is not null) return replay.Value;
        var accounts = await BookAccountsAsync(code, ct);
        var from = MoneyAccount(accounts, command.FromAccount);
        MoneyAccount(accounts, command.ToAccount);
        await RequireCashAsync(from, command.AmountSatang, ct);
        var posted = await new AccountingPostingService(database).PostAsync(new(code, "cash_transfer", command.Evidence,
            command.Date, command.RequestToken, fingerprint, Actor(actor),
            [new(command.ToAccount, command.AmountSatang, 0), new(command.FromAccount, 0, command.AmountSatang)],
            new("cash_transfer", command.RequestToken, JsonSerializer.Serialize(command))), ct);
        await tx.CommitAsync(ct);
        return posted.JournalId;
    }

    private async Task<Dictionary<string, AccountingAccount>> BookAccountsAsync(AccountingBookCode code, CancellationToken ct)
    {
        var book = await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == code, ct);
        if (book is null || !book.IsActivated)
            throw new AccountingValidationException("กรุณาเปิดใช้งานสมุดบัญชีด้วยยอดยกมาก่อนบันทึกรายการ");
        return await database.AccountingAccounts.Where(x => x.BookId == book.Id).ToDictionaryAsync(x => x.Code, ct);
    }

    private static AccountingAccount MoneyAccount(Dictionary<string, AccountingAccount> accounts, string code)
    {
        if (!accounts.TryGetValue(code, out var account) || account.Role is not (AccountingAccountRole.Cash or AccountingAccountRole.Bank))
            throw new AccountingValidationException("กรุณาเลือกเงินสดหรือธนาคารในสมุดบัญชีนี้");
        return account;
    }

    private async Task RequireCashAsync(AccountingAccount account, long amount, CancellationToken ct)
    {
        var lines = await database.AccountingJournalLines.Where(x => x.AccountId == account.Id).ToListAsync(ct);
        if (Sum(lines.Select(x => checked(x.DebitSatang - x.CreditSatang))) < amount)
            throw new AccountingValidationException("เงินคงเหลือในบัญชีต้นทางไม่เพียงพอ");
    }
}
