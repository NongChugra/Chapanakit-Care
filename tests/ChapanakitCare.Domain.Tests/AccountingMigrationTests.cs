using System.Data;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingMigrationTests
{
    private const string LatestPreAccountingMigration = "20260908160800_RecipientPhotosAndDurableHistory";
    private static readonly DateOnly BusinessDate = new(2026, 9, 8);
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_model_has_no_unmigrated_accounting_changes()
    {
        using var database = CreateContext("Data Source=:memory:");

        Assert.False(database.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Upgrade_from_pre_accounting_database_preserves_member_snapshot_and_unit_ledger()
    {
        var directory = Path.Combine(Path.GetTempPath(), "accounting-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "pre-accounting.db");
            await using var database = CreateContext($"Data Source={path};Pooling=False");
            await database.GetService<IMigrator>().MigrateAsync(LatestPreAccountingMigration);

            var expected = await SeedPreAccountingEvidenceAsync(database);
            database.ChangeTracker.Clear();

            await database.Database.MigrateAsync();
            database.ChangeTracker.Clear();

            var member = await database.Members.SingleAsync();
            var snapshot = await database.DeathMemberSnapshots.SingleAsync();
            var ledger = await database.AdvanceLedgerEntries.SingleAsync();

            Assert.Equal(expected.MemberId, member.Id);
            Assert.Equal("00001", member.RunNo);
            Assert.Equal(30, member.AdvanceUnitsBalance);
            Assert.Equal(expected.DeathCaseId, snapshot.DeathCaseId);
            Assert.Equal("00001", snapshot.RunNo);
            Assert.Equal(30, snapshot.AdvanceUnitsBeforeDeath);
            Assert.Equal(expected.LedgerId, ledger.Id);
            Assert.Equal(1, ledger.EntryOrder);
            Assert.Equal(30, ledger.BalanceAfter);
            Assert.False(database.Database.HasPendingModelChanges());
            Assert.Empty(await database.AccountingBooks.ToListAsync());
            Assert.True(await TableExistsAsync(database, "accounting_journals"));
            Assert.True(await TableExistsAsync(database, "welfare_collections"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Direct_ef_write_rejects_an_unbalanced_posted_journal_before_sqlite_receives_it()
    {
        await using var database = await MigratedDatabase.CreateAsync();
        var references = await SeedAccountingReferencesAsync(database.Context);
        var journal = Journal(references.WelfareBook, "W-00000001");
        database.Context.AccountingJournals.Add(journal);
        database.Context.AccountingJournalLines.AddRange(
            Line(journal, references.WelfareCash, 1, debitSatang: 100, creditSatang: 0),
            Line(journal, references.WelfareAdvanceLiability, 2, debitSatang: 0, creditSatang: 99));
        database.Context.AccountingPostingAudits.Add(Audit(journal));

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Context.SaveChangesAsync());

        database.Context.ChangeTracker.Clear();
        Assert.Empty(await database.Context.AccountingJournals.ToListAsync());
        Assert.Empty(await database.Context.AccountingJournalLines.ToListAsync());
    }

    [Fact]
    public async Task Direct_ef_write_rejects_mutating_or_deleting_append_only_accounting_history()
    {
        await using var database = await MigratedDatabase.CreateAsync();
        var references = await SeedAccountingReferencesAsync(database.Context);
        var posted = await PostBalancedJournalAsync(database.Context, references);

        posted.Journal.DescriptionSnapshot = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Context.SaveChangesAsync());

        database.Context.ChangeTracker.Clear();
        database.Context.AccountingJournals.Remove(await database.Context.AccountingJournals.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Context.SaveChangesAsync());

        database.Context.ChangeTracker.Clear();
        database.Context.AccountingJournalLines.Remove(await database.Context.AccountingJournalLines.FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Context.SaveChangesAsync());

        database.Context.ChangeTracker.Clear();
        database.Context.AccountingPostingAudits.Remove(await database.Context.AccountingPostingAudits.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Context.SaveChangesAsync());

        database.Context.ChangeTracker.Clear();
        database.Context.Set<WelfareCollection>().Remove(await database.Context.Set<WelfareCollection>().SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task SQLite_rejects_a_journal_line_that_uses_an_account_from_another_book()
    {
        await using var database = await MigratedDatabase.CreateAsync();
        var references = await SeedAccountingReferencesAsync(database.Context);
        var posted = await PostBalancedJournalAsync(database.Context, references);
        await database.Context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        var correctlyScopedRows = await database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounting_journal_lines
                (id, journal_id, book_id, account_id, line_no, debit_satang, credit_satang)
            VALUES
                ({Guid.NewGuid()}, {posted.Journal.Id}, {references.WelfareBook.Id}, {references.WelfareCash.Id}, {3}, {1L}, {0L})
            """);
        Assert.Equal(1, correctlyScopedRows);

        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounting_journal_lines
                (id, journal_id, book_id, account_id, line_no, debit_satang, credit_satang)
            VALUES
                ({Guid.NewGuid()}, {posted.Journal.Id}, {references.WelfareBook.Id}, {references.AssociationCash.Id}, {4}, {1L}, {0L})
            """));
    }

    [Fact]
    public async Task SQLite_rejects_a_reversal_that_targets_a_journal_in_another_book()
    {
        await using var database = await MigratedDatabase.CreateAsync();
        var references = await SeedAccountingReferencesAsync(database.Context);
        var posted = await PostBalancedJournalAsync(database.Context, references);
        var associationJournal = Journal(references.AssociationBook, "A-00000001");
        associationJournal.ReversesJournalId = posted.Journal.Id;
        await database.Context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");

        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounting_journals
                (id, book_id, operation_id, journal_number, voucher_type, description_snapshot,
                 business_date, recorded_at_utc, request_token, request_fingerprint, reverses_journal_id,
                 actor_user_id, actor_display_name, machine_name, app_version)
            VALUES
                ({associationJournal.Id}, {associationJournal.BookId}, {associationJournal.OperationId},
                 {associationJournal.JournalNumber}, {associationJournal.VoucherType}, {associationJournal.DescriptionSnapshot},
                 {associationJournal.BusinessDate}, {associationJournal.RecordedAtUtc}, {associationJournal.RequestToken},
                 {associationJournal.RequestFingerprint}, {associationJournal.ReversesJournalId},
                 {associationJournal.ActorUserId}, {associationJournal.ActorDisplayName}, {associationJournal.MachineName},
                 {associationJournal.AppVersion})
            """));
    }

    [Fact]
    public async Task SQLite_rejects_updates_and_deletes_of_append_only_accounting_history()
    {
        await using var database = await MigratedDatabase.CreateAsync();
        var references = await SeedAccountingReferencesAsync(database.Context);
        var posted = await PostBalancedJournalAsync(database.Context, references);

        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE accounting_journals SET description_snapshot = {"tampered"} WHERE id = {posted.Journal.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM accounting_journal_lines WHERE id = {posted.DebitLine.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM accounting_posting_audits WHERE id = {posted.Audit.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM welfare_collections WHERE id = {references.Collection.Id}"));
    }

    [Fact]
    public async Task SQLite_rejects_updates_and_deletes_of_accounting_audit_evidence()
    {
        await using var database = await MigratedDatabase.CreateAsync();
        var audit = new AuditEvent
        {
            Id = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            OccurredAtUtc = RecordedAtUtc,
            ActorUserId = "tester",
            ActorDisplayName = "Tester",
            MachineName = "test-machine",
            AppVersion = "test",
            Action = "accounting.reconciled",
            EntityType = "accounting_account",
            EntityId = Guid.NewGuid().ToString(),
            Reason = "statement evidence"
        };
        database.Context.AuditEvents.Add(audit);
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE audit_events SET reason = {"tampered"} WHERE id = {audit.Id}
            """));
        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM audit_events WHERE id = {audit.Id}
            """));
    }

    private static async Task<LegacyEvidence> SeedPreAccountingEvidenceAsync(AppDbContext database)
    {
        var member = TestData.Member();
        var deathCaseId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var deathCase = new DeathCase
        {
            Id = deathCaseId,
            DeathCaseNo = "D00001",
            DeathSequenceNo = 1,
            MemberId = member.Id,
            RecordedBusinessDate = BusinessDate,
            RecordedAtUtc = RecordedAtUtc,
            DeathCertificateNo = "DC-00001",
            DeathCertificateDate = BusinessDate,
            ReportedCertificateDate = BusinessDate,
            DeathCertificateFileName = "certificate.pdf",
            DeathCertificateContentType = "application/pdf",
            DeathCertificatePdf = "%PDF-1.4\n%%EOF"u8.ToArray(),
            DeathCertificateSize = "%PDF-1.4\n%%EOF"u8.Length,
            DeathCertificateSha256 = new string('a', 64),
            CauseOfDeathText = "legacy evidence",
            EligibilityResult = "payable",
            SettingsRevision = 1,
            ConfirmedAtUtc = RecordedAtUtc,
            ConfirmedBy = "legacy"
        };

        database.Members.Add(member);
        database.DeathCases.Add(deathCase);
        database.DeathMemberSnapshots.Add(new DeathMemberSnapshot
        {
            DeathCaseId = deathCaseId,
            RunNo = member.RunNo,
            FirstName = member.FirstName,
            LastName = member.LastName,
            District = member.District,
            Province = member.Province,
            ApplicationDate = member.ApplicationDate,
            ApprovalDate = member.ApprovalDate,
            CoverageStartDate = member.CoverageStartDate,
            AdvanceUnitsBeforeDeath = 30
        });
        database.DeathBeneficiarySnapshots.Add(new DeathBeneficiarySnapshot
        {
            Id = Guid.NewGuid(),
            DeathCaseId = deathCaseId,
            SlotNo = 1,
            FirstName = "ผู้รับ",
            LastName = "มรดก",
            ShareDenominator = 1
        });
        database.DeathCalculations.Add(new DeathCalculation
        {
            DeathCaseId = deathCaseId,
            IsPayable = true,
            ContributorCount = 1,
            WelfarePerMemberSatang = 900,
            GrossCollectionSatang = 900,
            ServiceFeeBasisPoints = 400,
            ServiceFeeRoundingMode = "round_down_to_baht",
            ServiceFeeSatang = 0,
            NetCollectionSatang = 900,
            DeceasedAdvanceUnits = 0,
            DeceasedAdvanceValueSatang = 0,
            TotalBenefitSatang = 900,
            BeneficiaryCount = 1,
            CalculatedAtUtc = RecordedAtUtc
        });
        database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry
        {
            Id = ledgerId,
            MemberId = member.Id,
            EntryOrder = 1,
            EntryType = "opening_30",
            BusinessDate = BusinessDate,
            UnitsDelta = 30,
            BalanceBefore = 0,
            BalanceAfter = 30,
            CreatedAtUtc = RecordedAtUtc,
            CreatedBy = "legacy"
        });
        await database.SaveChangesAsync();

        return new LegacyEvidence(member.Id, deathCaseId, ledgerId);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext database, string tableName)
    {
        var connection = database.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone)
            await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            var name = command.CreateParameter();
            name.ParameterName = "$name";
            name.Value = tableName;
            command.Parameters.Add(name);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        finally
        {
            if (closeWhenDone)
                await connection.CloseAsync();
        }
    }

    private static async Task<AccountingReferences> SeedAccountingReferencesAsync(AppDbContext database)
    {
        var member = TestData.Member();
        var welfareBook = Book(AccountingBookCode.Welfare, "บัญชีสวัสดิการ", "W");
        var associationBook = Book(AccountingBookCode.Association, "บัญชีสมาคม", "A");
        var welfareCash = Account(welfareBook, "1000", "เงินสด", AccountingAccountType.Asset,
            AccountingAccountRole.Cash, AccountingNormalBalance.Debit);
        var welfareAdvanceLiability = Account(welfareBook, "2000", "เงินสงเคราะห์ล่วงหน้า",
            AccountingAccountType.Liability, AccountingAccountRole.MemberAdvanceLiability, AccountingNormalBalance.Credit);
        var associationCash = Account(associationBook, "1000", "เงินสด", AccountingAccountType.Asset,
            AccountingAccountRole.Cash, AccountingNormalBalance.Debit);
        var collection = new WelfareCollection
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            MemberRunNo = member.RunNo,
            MemberName = "ทดสอบ สมาชิก",
            GroupNo = "01",
            CycleKey = "2026-09",
            BusinessDate = BusinessDate,
            DueDate = BusinessDate.AddDays(30),
            AmountSatang = 900,
            RequestToken = "collection-0001",
            RequestFingerprint = "collection-payload-0001",
            Description = "ใบเรียกเก็บทดสอบ",
            CreatedAtUtc = RecordedAtUtc,
            CreatedBy = "tester"
        };

        database.Members.Add(member);
        database.AccountingBooks.AddRange(welfareBook, associationBook);
        database.AccountingAccounts.AddRange(welfareCash, welfareAdvanceLiability, associationCash);
        database.Set<WelfareCollection>().Add(collection);
        await database.SaveChangesAsync();

        return new AccountingReferences(welfareBook, associationBook, welfareCash, welfareAdvanceLiability, associationCash, collection);
    }

    private static async Task<PostedJournal> PostBalancedJournalAsync(AppDbContext database, AccountingReferences references)
    {
        var journal = Journal(references.WelfareBook, "W-00000001");
        var debitLine = Line(journal, references.WelfareCash, 1, debitSatang: 100, creditSatang: 0);
        var creditLine = Line(journal, references.WelfareAdvanceLiability, 2, debitSatang: 0, creditSatang: 100);
        var audit = Audit(journal);
        database.AccountingJournals.Add(journal);
        database.AccountingJournalLines.AddRange(debitLine, creditLine);
        database.AccountingPostingAudits.Add(audit);
        await database.SaveChangesAsync();
        return new PostedJournal(journal, debitLine, audit);
    }

    private static AccountingBook Book(AccountingBookCode code, string name, string prefix) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = name,
        JournalPrefix = prefix,
        NextJournalNumber = 2,
        Version = 1,
        CreatedAtUtc = RecordedAtUtc,
        CreatedBy = "tester"
    };

    private static AccountingAccount Account(
        AccountingBook book,
        string code,
        string name,
        AccountingAccountType type,
        AccountingAccountRole role,
        AccountingNormalBalance normalBalance) => new()
    {
        Id = Guid.NewGuid(),
        BookId = book.Id,
        Code = code,
        Name = name,
        AccountType = type,
        Role = role,
        NormalBalance = normalBalance,
        CreatedAtUtc = RecordedAtUtc,
        CreatedBy = "tester"
    };

    private static AccountingJournal Journal(AccountingBook book, string number) => new()
    {
        Id = Guid.NewGuid(),
        BookId = book.Id,
        OperationId = Guid.NewGuid(),
        JournalNumber = number,
        VoucherType = "test",
        DescriptionSnapshot = "test journal",
        BusinessDate = BusinessDate,
        RecordedAtUtc = RecordedAtUtc,
        RequestToken = "journal-token-" + number,
        RequestFingerprint = "journal-payload-" + number,
        ActorUserId = "tester",
        ActorDisplayName = "Tester",
        MachineName = "test-machine",
        AppVersion = "test"
    };

    private static AccountingJournalLine Line(
        AccountingJournal journal,
        AccountingAccount account,
        int lineNo,
        long debitSatang,
        long creditSatang) => new()
    {
        Id = Guid.NewGuid(),
        JournalId = journal.Id,
        BookId = journal.BookId,
        AccountId = account.Id,
        LineNo = lineNo,
        DebitSatang = debitSatang,
        CreditSatang = creditSatang
    };

    private static AccountingPostingAudit Audit(AccountingJournal journal) => new()
    {
        Id = Guid.NewGuid(),
        JournalId = journal.Id,
        OperationId = journal.OperationId,
        OccurredAtUtc = RecordedAtUtc,
        Action = "posted",
        ActorUserId = journal.ActorUserId,
        ActorDisplayName = journal.ActorDisplayName,
        MachineName = journal.MachineName,
        AppVersion = journal.AppVersion,
        DetailsSnapshot = journal.DescriptionSnapshot
    };

    private static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);

    private sealed record LegacyEvidence(Guid MemberId, Guid DeathCaseId, Guid LedgerId);

    private sealed record AccountingReferences(
        AccountingBook WelfareBook,
        AccountingBook AssociationBook,
        AccountingAccount WelfareCash,
        AccountingAccount WelfareAdvanceLiability,
        AccountingAccount AssociationCash,
        WelfareCollection Collection);

    private sealed record PostedJournal(
        AccountingJournal Journal,
        AccountingJournalLine DebitLine,
        AccountingPostingAudit Audit);

    private sealed class MigratedDatabase : IAsyncDisposable
    {
        private readonly string directory;

        private MigratedDatabase(string directory, AppDbContext context)
        {
            this.directory = directory;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<MigratedDatabase> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "accounting-integrity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var context = CreateContext($"Data Source={Path.Combine(directory, "accounting.db")};Pooling=False");
                await context.Database.MigrateAsync();
                return new MigratedDatabase(directory, context);
            }
            catch
            {
                Directory.Delete(directory, recursive: true);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
