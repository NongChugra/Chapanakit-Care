using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.AccountingReports;

/// <summary>
/// Reads immutable accounting journals into book-specific report snapshots.
/// This service never creates, updates, or deletes accounting data.
/// </summary>
public sealed class AccountingReportQueryService
{
    private readonly AppDbContext database;

    public AccountingReportQueryService(AppDbContext database)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<AccountingTrialBalanceReport> GetTrialBalanceAsync(
        AccountingBookCode bookCode,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var book = await GetBookAsync(bookCode, cancellationToken);
        var accounts = await database.AccountingAccounts
            .AsNoTracking()
            .Where(account => account.BookId == book.Id)
            .OrderBy(account => account.Code)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountType,
                account.NormalBalance
            })
            .ToListAsync(cancellationToken);

        var amountsByAccount = await (
                from line in database.AccountingJournalLines.AsNoTracking()
                join journal in database.AccountingJournals.AsNoTracking()
                    on line.JournalId equals journal.Id
                where line.BookId == book.Id && journal.BookId == book.Id && journal.BusinessDate <= asOfDate
                group line by line.AccountId into lines
                select new
                {
                    AccountId = lines.Key,
                    DebitSatang = lines.Sum(line => line.DebitSatang),
                    CreditSatang = lines.Sum(line => line.CreditSatang)
                })
            .ToDictionaryAsync(amount => amount.AccountId, cancellationToken);

        var rows = new List<AccountingTrialBalanceRow>(accounts.Count);
        long totalDebit = 0;
        long totalCredit = 0;
        foreach (var account in accounts)
        {
            amountsByAccount.TryGetValue(account.Id, out var amounts);
            var debit = amounts?.DebitSatang ?? 0;
            var credit = amounts?.CreditSatang ?? 0;
            var net = checked(debit - credit);
            var debitBalance = net > 0 ? net : 0;
            var creditBalance = net < 0 ? checked(-net) : 0;
            rows.Add(new AccountingTrialBalanceRow(
                account.Code,
                account.Name,
                account.AccountType,
                account.NormalBalance,
                debitBalance,
                creditBalance));
            totalDebit = checked(totalDebit + debitBalance);
            totalCredit = checked(totalCredit + creditBalance);
        }

        return new AccountingTrialBalanceReport(
            book.Code,
            book.Name,
            asOfDate,
            rows,
            totalDebit,
            totalCredit);
    }

    public async Task<AccountingJournalReport> GetJournalAsync(
        AccountingBookCode bookCode,
        AccountingReportPeriod period,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(period);
        var book = await GetBookAsync(bookCode, cancellationToken);
        var lines = await (
                from journal in database.AccountingJournals.AsNoTracking()
                join line in database.AccountingJournalLines.AsNoTracking()
                    on journal.Id equals line.JournalId
                join account in database.AccountingAccounts.AsNoTracking()
                    on line.AccountId equals account.Id
                where journal.BookId == book.Id && line.BookId == book.Id && account.BookId == book.Id &&
                      journal.BusinessDate >= period.From && journal.BusinessDate <= period.To
                orderby journal.BusinessDate, journal.JournalNumber, line.LineNo
                select new JournalLineSnapshot(
                    journal.Id,
                    journal.JournalNumber,
                    journal.VoucherType,
                    journal.DescriptionSnapshot,
                    journal.BusinessDate,
                    journal.ReversesJournalId != null,
                    line.LineNo,
                    account.Code,
                    account.Name,
                    line.DebitSatang,
                    line.CreditSatang,
                    line.MemberId,
                    line.DeathCaseId,
                    line.BeneficiarySlotNo,
                    line.PartySnapshot,
                    line.DescriptionSnapshot))
            .ToListAsync(cancellationToken);

        var entries = lines
            .GroupBy(line => line.JournalId)
            .OrderBy(group => group.First().BusinessDate)
            .ThenBy(group => group.First().JournalNumber, StringComparer.Ordinal)
            .Select(group =>
            {
                var header = group.First();
                return new AccountingJournalEntryReport(
                    header.JournalId,
                    header.JournalNumber,
                    header.VoucherType,
                    header.Description,
                    header.BusinessDate,
                    header.IsReversal,
                    group.OrderBy(line => line.LineNo)
                        .Select(line => new AccountingJournalLineReport(
                            line.LineNo,
                            line.AccountCode,
                            line.AccountName,
                            line.DebitSatang,
                            line.CreditSatang,
                            line.MemberId,
                            line.DeathCaseId,
                            line.BeneficiarySlotNo,
                            line.PartySnapshot,
                            line.DescriptionSnapshot))
                        .ToList());
            })
            .ToList();

        return new AccountingJournalReport(
            book.Code,
            book.Name,
            period,
            entries,
            SumSatang(lines.Select(line => line.DebitSatang)),
            SumSatang(lines.Select(line => line.CreditSatang)));
    }

    public async Task<AccountingGeneralLedgerReport> GetGeneralLedgerAsync(
        AccountingBookCode bookCode,
        string accountCode,
        AccountingReportPeriod period,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(period);
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new AccountingValidationException("ต้องระบุรหัสบัญชีสำหรับบัญชีแยกประเภท");

        var book = await GetBookAsync(bookCode, cancellationToken);
        var normalizedAccountCode = accountCode.Trim();
        var account = await database.AccountingAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.BookId == book.Id && value.Code == normalizedAccountCode,
                cancellationToken)
            ?? throw new AccountingValidationException("ไม่พบรหัสบัญชีในสมุดบัญชีที่เลือก");

        var lines = await (
                from journal in database.AccountingJournals.AsNoTracking()
                join line in database.AccountingJournalLines.AsNoTracking()
                    on journal.Id equals line.JournalId
                where journal.BookId == book.Id && line.BookId == book.Id && line.AccountId == account.Id &&
                      journal.BusinessDate <= period.To
                select new GeneralLedgerLineSnapshot(
                    journal.BusinessDate,
                    journal.JournalNumber,
                    journal.VoucherType,
                    journal.DescriptionSnapshot,
                    line.LineNo,
                    line.DebitSatang,
                    line.CreditSatang))
            .ToListAsync(cancellationToken);

        var opening = ToBalanceSides(
            SumSatang(lines.Where(line => line.BusinessDate < period.From).Select(line => line.DebitSatang)),
            SumSatang(lines.Where(line => line.BusinessDate < period.From).Select(line => line.CreditSatang)));
        var movement = lines
            .Where(line => line.BusinessDate >= period.From)
            .OrderBy(line => line.BusinessDate)
            .ThenBy(line => line.JournalNumber, StringComparer.Ordinal)
            .ThenBy(line => line.LineNo)
            .ToList();

        var runningNet = checked(opening.DebitSatang - opening.CreditSatang);
        var entries = new List<AccountingGeneralLedgerEntry>(movement.Count);
        foreach (var line in movement)
        {
            runningNet = checked(runningNet + line.DebitSatang - line.CreditSatang);
            var running = ToBalanceSides(runningNet, 0);
            entries.Add(new AccountingGeneralLedgerEntry(
                line.BusinessDate,
                line.JournalNumber,
                line.VoucherType,
                line.Description,
                line.LineNo,
                line.DebitSatang,
                line.CreditSatang,
                running.DebitSatang,
                running.CreditSatang));
        }

        var closing = ToBalanceSides(runningNet, 0);
        return new AccountingGeneralLedgerReport(
            book.Code,
            book.Name,
            account.Code,
            account.Name,
            period,
            opening.DebitSatang,
            opening.CreditSatang,
            SumSatang(movement.Select(line => line.DebitSatang)),
            SumSatang(movement.Select(line => line.CreditSatang)),
            closing.DebitSatang,
            closing.CreditSatang,
            entries);
    }

    public async Task<AccountingIncomeExpenseReport> GetIncomeExpenseAsync(
        AccountingBookCode bookCode,
        AccountingReportPeriod period,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(period);
        var book = await GetBookAsync(bookCode, cancellationToken);
        var accounts = await database.AccountingAccounts
            .AsNoTracking()
            .Where(account => account.BookId == book.Id &&
                (account.AccountType == AccountingAccountType.Income || account.AccountType == AccountingAccountType.Expense))
            .OrderBy(account => account.Code)
            .Select(account => new AccountSnapshot(account.Id, account.Code, account.Name, account.AccountType))
            .ToListAsync(cancellationToken);
        var amountsByAccount = await GetAmountsByAccountAsync(book.Id, period.To, period.From, cancellationToken);

        var rows = new List<AccountingIncomeExpenseRow>(accounts.Count);
        long totalIncome = 0;
        long totalExpense = 0;
        foreach (var account in accounts)
        {
            amountsByAccount.TryGetValue(account.Id, out var amounts);
            var debit = amounts.DebitSatang;
            var credit = amounts.CreditSatang;
            var net = account.AccountType == AccountingAccountType.Income
                ? checked(credit - debit)
                : checked(debit - credit);
            rows.Add(new AccountingIncomeExpenseRow(
                account.Code,
                account.Name,
                account.AccountType,
                debit,
                credit,
                net));

            if (account.AccountType == AccountingAccountType.Income)
                totalIncome = checked(totalIncome + net);
            else
                totalExpense = checked(totalExpense + net);
        }

        return new AccountingIncomeExpenseReport(
            book.Code,
            book.Name,
            period,
            rows,
            totalIncome,
            totalExpense,
            checked(totalIncome - totalExpense));
    }

    public async Task<AccountingFinancialPositionReport> GetFinancialPositionAsync(
        AccountingBookCode bookCode,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var book = await GetBookAsync(bookCode, cancellationToken);
        var accounts = await database.AccountingAccounts
            .AsNoTracking()
            .Where(account => account.BookId == book.Id)
            .OrderBy(account => account.Code)
            .Select(account => new AccountSnapshot(account.Id, account.Code, account.Name, account.AccountType))
            .ToListAsync(cancellationToken);
        var amountsByAccount = await GetAmountsByAccountAsync(book.Id, asOfDate, null, cancellationToken);

        var assets = new List<AccountingFinancialPositionLine>();
        var liabilities = new List<AccountingFinancialPositionLine>();
        var equity = new List<AccountingFinancialPositionLine>();
        long currentResult = 0;

        foreach (var account in accounts)
        {
            amountsByAccount.TryGetValue(account.Id, out var amounts);
            var debit = amounts.DebitSatang;
            var credit = amounts.CreditSatang;
            switch (account.AccountType)
            {
                case AccountingAccountType.Asset:
                    assets.Add(new AccountingFinancialPositionLine(
                        account.Code, account.Name, account.AccountType, checked(debit - credit)));
                    break;
                case AccountingAccountType.Liability:
                    liabilities.Add(new AccountingFinancialPositionLine(
                        account.Code, account.Name, account.AccountType, checked(credit - debit)));
                    break;
                case AccountingAccountType.Equity:
                    equity.Add(new AccountingFinancialPositionLine(
                        account.Code, account.Name, account.AccountType, checked(credit - debit)));
                    break;
                case AccountingAccountType.Income:
                    currentResult = checked(currentResult + credit - debit);
                    break;
                case AccountingAccountType.Expense:
                    currentResult = checked(currentResult - debit + credit);
                    break;
                default:
                    throw new AccountingValidationException("ประเภทรหัสบัญชีไม่ถูกต้อง");
            }
        }

        var totalAssets = SumSatang(assets.Select(line => line.AmountSatang));
        var totalLiabilitiesAndEquity = checked(
            SumSatang(liabilities.Select(line => line.AmountSatang)) +
            SumSatang(equity.Select(line => line.AmountSatang)) +
            currentResult);

        return new AccountingFinancialPositionReport(
            book.Code,
            book.Name,
            asOfDate,
            assets,
            liabilities,
            equity,
            currentResult,
            totalAssets,
            totalLiabilitiesAndEquity,
            totalAssets == totalLiabilitiesAndEquity);
    }

    public async Task<AccountingMemberBalancesReport> GetMemberBalancesAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var welfareBook = await GetBookAsync(AccountingBookCode.Welfare, cancellationToken);
        var controlLines = await (
                from journal in database.AccountingJournals.AsNoTracking()
                join line in database.AccountingJournalLines.AsNoTracking()
                    on journal.Id equals line.JournalId
                join account in database.AccountingAccounts.AsNoTracking()
                    on line.AccountId equals account.Id
                where journal.BookId == welfareBook.Id && line.BookId == welfareBook.Id && account.BookId == welfareBook.Id &&
                      journal.BusinessDate <= asOfDate && line.MemberId != null &&
                      (account.Code == "2000" || account.Code == "1200")
                select new MemberControlLineSnapshot(
                    line.MemberId!.Value,
                    account.Code,
                    journal.BusinessDate,
                    journal.JournalNumber,
                    line.LineNo,
                    line.DebitSatang,
                    line.CreditSatang,
                    line.PartySnapshot))
            .ToListAsync(cancellationToken);

        var memberIds = controlLines.Select(line => line.MemberId).Distinct().ToArray();
        var members = memberIds.Length == 0
            ? new List<MemberSnapshot>()
            : await database.Members
                .AsNoTracking()
                .Where(member => memberIds.Contains(member.Id))
                .Select(member => new MemberSnapshot(member.Id, member.RunNo, member.Title, member.FirstName, member.LastName))
                .ToListAsync(cancellationToken);
        var membersById = members.ToDictionary(member => member.Id);

        var rows = new List<AccountingMemberBalanceRow>();
        foreach (var group in controlLines.GroupBy(line => line.MemberId))
        {
            long advance = 0;
            long shortfall = 0;
            foreach (var line in group)
            {
                if (line.AccountCode == "2000")
                    advance = checked(advance + line.CreditSatang - line.DebitSatang);
                else
                    shortfall = checked(shortfall + line.DebitSatang - line.CreditSatang);
            }

            var snapshot = LatestPartySnapshot(group);
            var display = membersById.TryGetValue(group.Key, out var member)
                ? new MemberDisplay(member.RunNo, ComposeName(member.Title, member.FirstName, member.LastName))
                : ParseMemberSnapshot(snapshot);
            rows.Add(new AccountingMemberBalanceRow(
                group.Key,
                display.RunNo,
                display.Name,
                advance,
                shortfall,
                checked(advance - shortfall)));
        }

        var orderedRows = rows
            .OrderBy(row => row.MemberRunNo, StringComparer.Ordinal)
            .ThenBy(row => row.MemberName, StringComparer.Ordinal)
            .ThenBy(row => row.MemberId)
            .ToList();
        var totalAdvance = SumSatang(orderedRows.Select(row => row.AdvanceSatang));
        var totalShortfall = SumSatang(orderedRows.Select(row => row.ShortfallSatang));
        return new AccountingMemberBalancesReport(
            asOfDate,
            orderedRows,
            totalAdvance,
            totalShortfall,
            checked(totalAdvance - totalShortfall));
    }

    public async Task<AccountingBeneficiaryUnpaidReport> GetBeneficiaryUnpaidAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var welfareBook = await GetBookAsync(AccountingBookCode.Welfare, cancellationToken);
        var controlLines = await (
                from journal in database.AccountingJournals.AsNoTracking()
                join line in database.AccountingJournalLines.AsNoTracking()
                    on journal.Id equals line.JournalId
                join account in database.AccountingAccounts.AsNoTracking()
                    on line.AccountId equals account.Id
                where journal.BookId == welfareBook.Id && line.BookId == welfareBook.Id && account.BookId == welfareBook.Id &&
                      journal.BusinessDate <= asOfDate && account.Code == "2100"
                select new BeneficiaryControlLineSnapshot(
                    line.DeathCaseId,
                    line.BeneficiarySlotNo,
                    journal.BusinessDate,
                    journal.JournalNumber,
                    line.LineNo,
                    line.DebitSatang,
                    line.CreditSatang,
                    line.PartySnapshot))
            .ToListAsync(cancellationToken);

        var deathCaseIds = controlLines
            .Where(line => line.DeathCaseId is not null)
            .Select(line => line.DeathCaseId!.Value)
            .Distinct()
            .ToArray();
        var snapshots = deathCaseIds.Length == 0
            ? new List<DeathBeneficiarySnapshot>()
            : await database.DeathBeneficiarySnapshots
                .AsNoTracking()
                .Where(snapshot => deathCaseIds.Contains(snapshot.DeathCaseId))
                .ToListAsync(cancellationToken);
        var snapshotsByDimension = snapshots.ToDictionary(
            snapshot => (snapshot.DeathCaseId, snapshot.SlotNo));

        var rows = new List<AccountingBeneficiaryUnpaidRow>();
        foreach (var group in controlLines.GroupBy(line => new
                 {
                     DeathCaseId = line.DeathCaseId ?? Guid.Empty,
                     BeneficiarySlotNo = line.BeneficiarySlotNo ?? 0
                 }))
        {
            var payable = SumSatang(group.Select(line => line.CreditSatang));
            var paid = SumSatang(group.Select(line => line.DebitSatang));
            var snapshot = LatestPartySnapshot(group);
            var beneficiaryName = snapshotsByDimension.TryGetValue(
                (group.Key.DeathCaseId, group.Key.BeneficiarySlotNo), out var beneficiary)
                ? ComposeName(beneficiary.Title, beneficiary.FirstName, beneficiary.LastName)
                : string.IsNullOrWhiteSpace(snapshot) ? "ไม่ระบุผู้รับผลประโยชน์" : snapshot.Trim();
            rows.Add(new AccountingBeneficiaryUnpaidRow(
                group.Key.DeathCaseId,
                group.Key.BeneficiarySlotNo,
                beneficiaryName,
                payable,
                paid,
                checked(payable - paid)));
        }

        var orderedRows = rows
            .OrderBy(row => row.DeathCaseId)
            .ThenBy(row => row.BeneficiarySlotNo)
            .ToList();
        var totalPayable = SumSatang(orderedRows.Select(row => row.PayableSatang));
        var totalPaid = SumSatang(orderedRows.Select(row => row.PaidSatang));
        return new AccountingBeneficiaryUnpaidReport(
            asOfDate,
            orderedRows,
            totalPayable,
            totalPaid,
            checked(totalPayable - totalPaid));
    }

    private async Task<Dictionary<Guid, AccountAmounts>> GetAmountsByAccountAsync(
        Guid bookId,
        DateOnly to,
        DateOnly? from,
        CancellationToken cancellationToken)
    {
        var query =
            from line in database.AccountingJournalLines.AsNoTracking()
            join journal in database.AccountingJournals.AsNoTracking()
                on line.JournalId equals journal.Id
            where line.BookId == bookId && journal.BookId == bookId && journal.BusinessDate <= to
            select new { line, journal };

        if (from is { } fromDate)
            query = query.Where(value => value.journal.BusinessDate >= fromDate);

        return await query
            .GroupBy(value => value.line.AccountId)
            .Select(lines => new
            {
                AccountId = lines.Key,
                DebitSatang = lines.Sum(value => value.line.DebitSatang),
                CreditSatang = lines.Sum(value => value.line.CreditSatang)
            })
            .ToDictionaryAsync(
                amounts => amounts.AccountId,
                amounts => new AccountAmounts(amounts.DebitSatang, amounts.CreditSatang),
                cancellationToken);
    }

    private static void ValidatePeriod(AccountingReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        period.Validate();
    }

    private static BalanceSides ToBalanceSides(long debitSatang, long creditSatang)
    {
        var net = checked(debitSatang - creditSatang);
        return net >= 0
            ? new BalanceSides(net, 0)
            : new BalanceSides(0, checked(-net));
    }

    private static long SumSatang(IEnumerable<long> amounts)
    {
        long total = 0;
        foreach (var amount in amounts)
            total = checked(total + amount);
        return total;
    }

    private static string? LatestPartySnapshot(IEnumerable<MemberControlLineSnapshot> lines) => lines
        .OrderByDescending(line => line.BusinessDate)
        .ThenByDescending(line => line.JournalNumber, StringComparer.Ordinal)
        .ThenByDescending(line => line.LineNo)
        .Select(line => line.PartySnapshot)
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? LatestPartySnapshot(IEnumerable<BeneficiaryControlLineSnapshot> lines) => lines
        .OrderByDescending(line => line.BusinessDate)
        .ThenByDescending(line => line.JournalNumber, StringComparer.Ordinal)
        .ThenByDescending(line => line.LineNo)
        .Select(line => line.PartySnapshot)
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static MemberDisplay ParseMemberSnapshot(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
            return new MemberDisplay("ไม่ทราบ", "ไม่ระบุสมาชิก");

        var text = snapshot.Trim();
        var separator = text.IndexOfAny([' ', '\t', '\r', '\n']);
        if (separator > 0)
        {
            var runNo = text[..separator];
            var name = text[(separator + 1)..].Trim();
            if (runNo.Length == 5 && runNo.All(char.IsDigit) && name.Length > 0)
                return new MemberDisplay(runNo, name);
        }

        return new MemberDisplay("ไม่ทราบ", text);
    }

    private static string ComposeName(string? title, string? firstName, string? lastName) => string.Join(
        " ",
        new[] { title, firstName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

    private async Task<AccountingBook> GetBookAsync(
        AccountingBookCode bookCode,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(bookCode))
            throw new AccountingValidationException("ไม่พบสมุดบัญชีที่เลือก");

        return await database.AccountingBooks
            .AsNoTracking()
            .SingleOrDefaultAsync(book => book.Code == bookCode, cancellationToken)
            ?? throw new AccountingValidationException("ยังไม่ได้จัดเตรียมสมุดบัญชีที่เลือก");
    }

    private sealed record JournalLineSnapshot(
        Guid JournalId,
        string JournalNumber,
        string VoucherType,
        string Description,
        DateOnly BusinessDate,
        bool IsReversal,
        int LineNo,
        string AccountCode,
        string AccountName,
        long DebitSatang,
        long CreditSatang,
        Guid? MemberId,
        Guid? DeathCaseId,
        int? BeneficiarySlotNo,
        string? PartySnapshot,
        string? DescriptionSnapshot);

    private sealed record GeneralLedgerLineSnapshot(
        DateOnly BusinessDate,
        string JournalNumber,
        string VoucherType,
        string Description,
        int LineNo,
        long DebitSatang,
        long CreditSatang);

    private sealed record AccountSnapshot(
        Guid Id,
        string Code,
        string Name,
        AccountingAccountType AccountType);

    private readonly record struct AccountAmounts(long DebitSatang, long CreditSatang);

    private readonly record struct BalanceSides(long DebitSatang, long CreditSatang);

    private sealed record MemberControlLineSnapshot(
        Guid MemberId,
        string AccountCode,
        DateOnly BusinessDate,
        string JournalNumber,
        int LineNo,
        long DebitSatang,
        long CreditSatang,
        string? PartySnapshot);

    private sealed record MemberSnapshot(
        Guid Id,
        string RunNo,
        string? Title,
        string FirstName,
        string LastName);

    private sealed record MemberDisplay(string RunNo, string Name);

    private sealed record BeneficiaryControlLineSnapshot(
        Guid? DeathCaseId,
        int? BeneficiarySlotNo,
        DateOnly BusinessDate,
        string JournalNumber,
        int LineNo,
        long DebitSatang,
        long CreditSatang,
        string? PartySnapshot);
}
