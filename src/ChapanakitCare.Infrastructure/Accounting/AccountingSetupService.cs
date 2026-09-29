using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChapanakitCare.Infrastructure.Accounting;

public sealed class AccountingSetupService
{
    public async Task<AccountingAccount> AddCashAccountAsync(AccountingCashAccountSetup setup, CancellationToken cancellationToken = default)
    {
        ValidateActor(setup.Actor);
        if (!Enum.IsDefined(setup.BookCode)) throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก");
        var code = NormalizeAdditionalAccountCode(setup.AccountCode, "10", "บัญชีเงินสดเพิ่มเติมต้องใช้รหัส 1001-1099");
        var name = RequireText(setup.AccountName, "ต้องระบุชื่อผู้ถือเงินสดหรือบัญชีเงินสด");
        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        var book = await FindBookAsync(setup.BookCode, cancellationToken);
        if (await database.AccountingAccounts.AnyAsync(x => x.BookId == book.Id && x.Code == code, cancellationToken))
            throw new AccountingValidationException("รหัสบัญชีเงินสดนี้มีอยู่ในสมุดบัญชีที่เลือกแล้ว");
        var account = new AccountingAccount
        {
            Id = Guid.NewGuid(), BookId = book.Id, Code = code, Name = name,
            AccountType = AccountingAccountType.Asset, Role = AccountingAccountRole.Cash,
            NormalBalance = AccountingNormalBalance.Debit, IsBankAccount = false,
            CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = setup.Actor.UserId.Trim()
        };
        database.AccountingAccounts.Add(account);
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return account;
    }
    private readonly AppDbContext database;

    public AccountingSetupService(AppDbContext database)
    {
        this.database = database;
    }

    public async Task EnsureCatalogAsync(CancellationToken cancellationToken = default)
    {
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var books = await database.AccountingBooks.ToDictionaryAsync(x => x.Code, cancellationToken);
            var welfare = GetOrAddBook(books, AccountingBookCode.Welfare, "บัญชีสวัสดิการสมาชิก", "W", now);
            var association = GetOrAddBook(books, AccountingBookCode.Association, "บัญชีสมาคม", "A", now);

            var accountKeys = await database.AccountingAccounts
                .Select(x => new { x.BookId, x.Code })
                .ToListAsync(cancellationToken);
            var existing = accountKeys.Select(x => (x.BookId, x.Code)).ToHashSet();
            AddMissingAccounts(welfare, existing, now);
            AddMissingAccounts(association, existing, now);

            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public Task<AccountingAccount> AddBankAccountAsync(
        AccountingBankAccountSetup setup,
        CancellationToken cancellationToken = default) =>
        AddBankAccountCoreAsync(setup, cancellationToken);

    public Task<AccountingAccount> AddExpenseCategoryAsync(
        AccountingExpenseCategorySetup setup,
        CancellationToken cancellationToken = default) =>
        AddExpenseCategoryCoreAsync(setup, cancellationToken);

    public Task ActivateBookAsync(
        AccountingBookActivation activation,
        CancellationToken cancellationToken = default) =>
        ActivateBookCoreAsync(activation, cancellationToken);

    public Task ClosePeriodAsync(
        AccountingPeriodCommand command,
        CancellationToken cancellationToken = default) =>
        ClosePeriodCoreAsync(command, cancellationToken);

    public Task ReopenPeriodAsync(
        AccountingPeriodCommand command,
        CancellationToken cancellationToken = default) =>
        ReopenPeriodCoreAsync(command, cancellationToken);

    private AccountingBook GetOrAddBook(
        IDictionary<AccountingBookCode, AccountingBook> books,
        AccountingBookCode code,
        string name,
        string prefix,
        DateTimeOffset now)
    {
        if (books.TryGetValue(code, out var existing))
            return existing;

        var book = new AccountingBook
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            JournalPrefix = prefix,
            NextJournalNumber = 1,
            Version = 1,
            CreatedAtUtc = now,
            CreatedBy = "accounting_catalog"
        };
        database.AccountingBooks.Add(book);
        books.Add(code, book);
        return book;
    }

    private void AddMissingAccounts(
        AccountingBook book,
        ISet<(Guid BookId, string Code)> existing,
        DateTimeOffset now)
    {
        foreach (var definition in AccountDefinitions(book.Code))
        {
            if (!existing.Add((book.Id, definition.Code)))
                continue;

            database.AccountingAccounts.Add(new AccountingAccount
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                Code = definition.Code,
                Name = definition.Name,
                AccountType = definition.AccountType,
                Role = definition.Role,
                NormalBalance = definition.NormalBalance,
                IsBankAccount = definition.IsBankAccount,
                BankDisplayName = definition.IsBankAccount ? "ยังไม่ระบุบัญชีธนาคาร" : null,
                CreatedAtUtc = now,
                CreatedBy = "accounting_catalog"
            });
        }
    }

    private static IReadOnlyList<AccountDefinition> AccountDefinitions(AccountingBookCode bookCode) =>
        bookCode == AccountingBookCode.Welfare
            ?
            [
                new("1000", "เงินสด", AccountingAccountType.Asset, AccountingAccountRole.Cash, AccountingNormalBalance.Debit, false),
                new("1100", "บัญชีธนาคาร", AccountingAccountType.Asset, AccountingAccountRole.Bank, AccountingNormalBalance.Debit, true),
                new("1200", "ลูกหนี้เงินสงเคราะห์สมาชิก", AccountingAccountType.Asset, AccountingAccountRole.MemberContributionShortfallReceivable, AccountingNormalBalance.Debit, false),
                new("2000", "เงินสงเคราะห์ล่วงหน้าสมาชิก", AccountingAccountType.Liability, AccountingAccountRole.MemberAdvanceLiability, AccountingNormalBalance.Credit, false),
                new("2100", "เงินสงเคราะห์ค้างจ่าย", AccountingAccountType.Liability, AccountingAccountRole.WelfareBenefitPayable, AccountingNormalBalance.Credit, false),
                new("2200", "ค่าหักร้อยละ 4 ค้างนำส่งสมาคม", AccountingAccountType.Liability, AccountingAccountRole.FeeDueToAssociation, AccountingNormalBalance.Credit, false),
                new("3000", "ทุนยกมาตามหลักฐาน", AccountingAccountType.Equity, AccountingAccountRole.OpeningAccumulatedFund, AccountingNormalBalance.Credit, false)
            ]
            :
            [
                new("1000", "เงินสด", AccountingAccountType.Asset, AccountingAccountRole.Cash, AccountingNormalBalance.Debit, false),
                new("1100", "บัญชีธนาคาร", AccountingAccountType.Asset, AccountingAccountRole.Bank, AccountingNormalBalance.Debit, true),
                new("1300", "ค่าหักร้อยละ 4 ค้างรับจากบัญชีสวัสดิการ", AccountingAccountType.Asset, AccountingAccountRole.DueFromWelfare, AccountingNormalBalance.Debit, false),
                new("3000", "ทุนยกมาตามหลักฐาน", AccountingAccountType.Equity, AccountingAccountRole.OpeningAccumulatedFund, AccountingNormalBalance.Credit, false),
                new("4000", "รายได้ค่าหักเงินสงเคราะห์ร้อยละ 4", AccountingAccountType.Income, AccountingAccountRole.WelfareDeductionIncome, AccountingNormalBalance.Credit, false),
                new("5000", "ค่าใช้จ่ายดำเนินงาน", AccountingAccountType.Expense, AccountingAccountRole.OperatingExpense, AccountingNormalBalance.Debit, false)
            ];

    private sealed record AccountDefinition(
        string Code,
        string Name,
        AccountingAccountType AccountType,
        AccountingAccountRole Role,
        AccountingNormalBalance NormalBalance,
        bool IsBankAccount);

    private async Task<AccountingAccount> AddBankAccountCoreAsync(
        AccountingBankAccountSetup setup,
        CancellationToken cancellationToken)
    {
        ValidateActor(setup.Actor);
        if (!Enum.IsDefined(setup.BookCode))
            throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก");

        var code = NormalizeAdditionalAccountCode(setup.AccountCode, "11", "บัญชีธนาคารเพิ่มเติมต้องใช้รหัส 11xx โดยไม่ใช้รหัสควบคุม 1100");
        var name = RequireText(setup.AccountName, "ต้องระบุชื่อบัญชีธนาคาร");
        var bankDisplayName = RequireText(setup.BankDisplayName, "ต้องระบุชื่อธนาคารหรือเลขอ้างอิงบัญชี");
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var book = await FindBookAsync(setup.BookCode, cancellationToken);
            if (await database.AccountingAccounts.AnyAsync(x => x.BookId == book.Id && x.Code == code, cancellationToken))
                throw new AccountingValidationException("รหัสบัญชีธนาคารนี้มีอยู่ในสมุดบัญชีที่เลือกแล้ว");

            var account = new AccountingAccount
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                Code = code,
                Name = name,
                AccountType = AccountingAccountType.Asset,
                Role = AccountingAccountRole.Bank,
                NormalBalance = AccountingNormalBalance.Debit,
                IsBankAccount = true,
                BankDisplayName = bankDisplayName,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = setup.Actor.UserId.Trim()
            };
            database.AccountingAccounts.Add(account);
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return account;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<AccountingAccount> AddExpenseCategoryCoreAsync(
        AccountingExpenseCategorySetup setup,
        CancellationToken cancellationToken)
    {
        ValidateActor(setup.Actor);
        var code = NormalizeAdditionalAccountCode(setup.AccountCode, "50", "หมวดค่าใช้จ่ายต้องใช้รหัส 50xx โดยไม่ใช้รหัสควบคุม 5000");
        var name = RequireText(setup.AccountName, "ต้องระบุชื่อหมวดค่าใช้จ่าย");
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var book = await FindBookAsync(AccountingBookCode.Association, cancellationToken);
            if (await database.AccountingAccounts.AnyAsync(x => x.BookId == book.Id && x.Code == code, cancellationToken))
                throw new AccountingValidationException("รหัสหมวดค่าใช้จ่ายนี้มีอยู่แล้ว");

            var account = new AccountingAccount
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                Code = code,
                Name = name,
                AccountType = AccountingAccountType.Expense,
                Role = AccountingAccountRole.OperatingExpense,
                NormalBalance = AccountingNormalBalance.Debit,
                IsBankAccount = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = setup.Actor.UserId.Trim()
            };
            database.AccountingAccounts.Add(account);
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return account;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task ClosePeriodCoreAsync(AccountingPeriodCommand command, CancellationToken cancellationToken)
    {
        ValidatePeriodCommand(command);
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var book = await FindActivatedBookAsync(command.BookCode, cancellationToken);
            var period = await database.AccountingPeriods.SingleOrDefaultAsync(x =>
                x.BookId == book.Id && x.StartsOn == command.StartsOn && x.EndsOn == command.EndsOn,
                cancellationToken);
            if (period is null)
            {
                var overlaps = await database.AccountingPeriods.AnyAsync(x => x.BookId == book.Id &&
                    x.StartsOn <= command.EndsOn && command.StartsOn <= x.EndsOn, cancellationToken);
                if (overlaps)
                    throw new AccountingValidationException("ช่วงงวดบัญชีซ้อนทับกับงวดที่มีอยู่แล้ว");

                period = new AccountingPeriod
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    StartsOn = command.StartsOn,
                    EndsOn = command.EndsOn,
                    Status = AccountingPeriodStatus.Closed,
                    ClosedAtUtc = DateTimeOffset.UtcNow,
                    ClosedBy = command.Actor.UserId.Trim(),
                    ClosureEvidence = command.Evidence.Trim(),
                    Version = 1
                };
                database.AccountingPeriods.Add(period);
            }
            else if (period.Status == AccountingPeriodStatus.Closed)
            {
                if (period.ClosureEvidence == command.Evidence.Trim())
                    return;
                throw new AccountingValidationException("งวดบัญชีนี้ปิดแล้วและใช้หลักฐานการปิดงวดเดิม");
            }
            else
            {
                period.Status = AccountingPeriodStatus.Closed;
                period.ClosedAtUtc = DateTimeOffset.UtcNow;
                period.ClosedBy = command.Actor.UserId.Trim();
                period.ClosureEvidence = command.Evidence.Trim();
                period.Version++;
            }

            AddPeriodAudit(period, command, "accounting.period_closed");
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task ReopenPeriodCoreAsync(AccountingPeriodCommand command, CancellationToken cancellationToken)
    {
        ValidatePeriodCommand(command);
        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var book = await FindActivatedBookAsync(command.BookCode, cancellationToken);
            var period = await database.AccountingPeriods.SingleOrDefaultAsync(x =>
                x.BookId == book.Id && x.StartsOn == command.StartsOn && x.EndsOn == command.EndsOn,
                cancellationToken)
                ?? throw new AccountingValidationException("ไม่พบงวดบัญชีที่ต้องการเปิดแก้ไข");
            if (period.Status != AccountingPeriodStatus.Closed)
            {
                if (period.ReopenReason == command.Evidence.Trim())
                    return;
                throw new AccountingValidationException("งวดบัญชีนี้ไม่ได้อยู่ในสถานะปิด");
            }

            period.Status = AccountingPeriodStatus.Open;
            period.ReopenedAtUtc = DateTimeOffset.UtcNow;
            period.ReopenedBy = command.Actor.UserId.Trim();
            period.ReopenReason = command.Evidence.Trim();
            period.Version++;
            AddPeriodAudit(period, command, "accounting.period_reopened");
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private void AddPeriodAudit(AccountingPeriod period, AccountingPeriodCommand command, string action)
    {
        database.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), OccurredAtUtc = DateTimeOffset.UtcNow,
            ActorUserId = command.Actor.UserId, ActorDisplayName = command.Actor.DisplayName,
            MachineName = command.Actor.MachineName, AppVersion = command.Actor.AppVersion,
            Action = action, EntityType = "accounting_period", EntityId = period.Id.ToString(),
            Reason = command.Evidence.Trim()
        });
    }

    private async Task ActivateBookCoreAsync(AccountingBookActivation activation, CancellationToken cancellationToken)
    {
        ValidateActor(activation.Actor);
        if (!Enum.IsDefined(activation.BookCode))
            throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก");
        if (string.IsNullOrWhiteSpace(activation.OpeningEvidence))
            throw new AccountingValidationException("ต้องระบุหลักฐานยอดยกมา แม้ยอดยกมาเป็นศูนย์");

        var ownsTransaction = database.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        try
        {
            if (ownsTransaction)
                transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var book = await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == activation.BookCode, cancellationToken)
                ?? throw new AccountingValidationException("ยังไม่ได้จัดเตรียมผังบัญชี");
            if (book.IsActivated)
            {
                if (book.CutoverStartDate == activation.CutoverStartDate && book.OpeningEvidence == activation.OpeningEvidence)
                    return;
                throw new AccountingValidationException("สมุดบัญชีนี้เปิดใช้งานแล้วและไม่สามารถเปลี่ยนวันตัดยอดได้");
            }

            book.IsActivated = true;
            book.CutoverStartDate = activation.CutoverStartDate;
            book.OpeningEvidence = activation.OpeningEvidence.Trim();
            book.ActivatedAtUtc = DateTimeOffset.UtcNow;
            book.ActivatedBy = activation.Actor.UserId.Trim();
            book.Version++;
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static void ValidateActor(AccountingActor actor)
    {
        if (string.IsNullOrWhiteSpace(actor.UserId) || string.IsNullOrWhiteSpace(actor.DisplayName) ||
            string.IsNullOrWhiteSpace(actor.MachineName) || string.IsNullOrWhiteSpace(actor.AppVersion))
            throw new AccountingValidationException("ต้องระบุผู้บันทึก เครื่อง และรุ่นโปรแกรมสำหรับรายการบัญชี");
    }

    private async Task<AccountingBook> FindBookAsync(AccountingBookCode code, CancellationToken cancellationToken) =>
        await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == code, cancellationToken)
        ?? throw new AccountingValidationException("ยังไม่ได้จัดเตรียมผังบัญชี");

    private async Task<AccountingBook> FindActivatedBookAsync(AccountingBookCode code, CancellationToken cancellationToken)
    {
        var book = await FindBookAsync(code, cancellationToken);
        if (!book.IsActivated || book.CutoverStartDate is null)
            throw new AccountingValidationException("สมุดบัญชียังไม่เปิดใช้งานด้วยยอดยกมาตามหลักฐาน");
        return book;
    }

    private static string NormalizeAdditionalAccountCode(string? value, string prefix, string message)
    {
        var code = value?.Trim() ?? string.Empty;
        if (code.Length != 4 || !code.StartsWith(prefix, StringComparison.Ordinal) || code.EndsWith("00", StringComparison.Ordinal) ||
            code.Any(character => character is < '0' or > '9'))
            throw new AccountingValidationException(message);
        return code;
    }

    private static string RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AccountingValidationException(message);
        return value.Trim();
    }

    private static void ValidatePeriodCommand(AccountingPeriodCommand command)
    {
        ValidateActor(command.Actor);
        if (!Enum.IsDefined(command.BookCode))
            throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก");
        if (command.StartsOn > command.EndsOn)
            throw new AccountingValidationException("วันเริ่มงวดบัญชีต้องไม่เกินวันสิ้นสุดงวด");
        _ = RequireText(command.Evidence, "ต้องระบุหลักฐานการปิดหรือเปิดแก้ไขงวดบัญชี");
    }
}
