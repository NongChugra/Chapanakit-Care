using ChapanakitCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    public DbSet<AccountingBook> AccountingBooks => Set<AccountingBook>();
    public DbSet<AccountingAccount> AccountingAccounts => Set<AccountingAccount>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<AccountingJournal> AccountingJournals => Set<AccountingJournal>();
    public DbSet<AccountingJournalLine> AccountingJournalLines => Set<AccountingJournalLine>();
    public DbSet<AccountingPostingAudit> AccountingPostingAudits => Set<AccountingPostingAudit>();

    private static void ConfigureAccounting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountingBook>(entity =>
        {
            entity.ToTable("accounting_books", table =>
            {
                table.HasCheckConstraint("ck_accounting_books_next_number", "next_journal_number >= 1");
                table.HasCheckConstraint("ck_accounting_books_version", "version >= 1");
                table.HasCheckConstraint("ck_accounting_books_activation", "is_activated IN (0, 1)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Code).HasConversion<string>().IsRequired();
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.JournalPrefix).IsRequired();
            entity.Property(x => x.CreatedBy).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<AccountingAccount>(entity =>
        {
            entity.ToTable("accounting_accounts", table =>
            {
                table.HasCheckConstraint("ck_accounting_accounts_code", "length(trim(code)) > 0 AND code = trim(code)");
                table.HasCheckConstraint("ck_accounting_accounts_bank", "is_bank_account IN (0, 1)");
                table.HasCheckConstraint("ck_accounting_accounts_type", "account_type IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense')");
                table.HasCheckConstraint("ck_accounting_accounts_normal_balance", "normal_balance IN ('Debit', 'Credit')");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.BookId });
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Code).IsRequired();
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.AccountType).HasConversion<string>().IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().IsRequired();
            entity.Property(x => x.NormalBalance).HasConversion<string>().IsRequired();
            entity.Property(x => x.CreatedBy).IsRequired();
            entity.HasIndex(x => new { x.BookId, x.Code }).IsUnique();
            entity.HasOne<AccountingBook>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountingPeriod>(entity =>
        {
            entity.ToTable("accounting_periods", table =>
            {
                table.HasCheckConstraint("ck_accounting_periods_dates", "ends_on >= starts_on");
                table.HasCheckConstraint("ck_accounting_periods_status", "status IN ('Open', 'Closed')");
                table.HasCheckConstraint("ck_accounting_periods_version", "version >= 1");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Status).HasConversion<string>().IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => new { x.BookId, x.StartsOn, x.EndsOn }).IsUnique();
            entity.HasIndex(x => new { x.BookId, x.Status, x.StartsOn, x.EndsOn });
            entity.HasOne<AccountingBook>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountingJournal>(entity =>
        {
            entity.ToTable("accounting_journals", table =>
            {
                table.HasCheckConstraint("ck_accounting_journals_number", "length(trim(journal_number)) > 0 AND journal_number = trim(journal_number)");
                table.HasCheckConstraint("ck_accounting_journals_token", "length(trim(request_token)) > 0 AND request_token = trim(request_token)");
                table.HasCheckConstraint("ck_accounting_journals_fingerprint", "length(trim(request_fingerprint)) > 0");
                table.HasCheckConstraint("ck_accounting_journals_reversal", "reverses_journal_id IS NULL OR reverses_journal_id <> id");
                table.HasCheckConstraint("ck_accounting_journals_voucher", "length(trim(voucher_type)) > 0");
                table.HasCheckConstraint("ck_accounting_journals_description", "length(trim(description_snapshot)) > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.BookId });
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.JournalNumber).IsRequired();
            entity.Property(x => x.VoucherType).IsRequired();
            entity.Property(x => x.DescriptionSnapshot).IsRequired();
            entity.Property(x => x.RequestToken).IsRequired();
            entity.Property(x => x.RequestFingerprint).IsRequired();
            entity.Property(x => x.ActorUserId).IsRequired();
            entity.Property(x => x.ActorDisplayName).IsRequired();
            entity.Property(x => x.MachineName).IsRequired();
            entity.Property(x => x.AppVersion).IsRequired();
            entity.HasIndex(x => new { x.BookId, x.RequestToken }).IsUnique();
            entity.HasIndex(x => new { x.BookId, x.JournalNumber }).IsUnique();
            entity.HasIndex(x => new { x.BookId, x.BusinessDate });
            entity.HasIndex(x => x.ReversesJournalId).IsUnique();
            entity.HasOne<AccountingBook>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountingJournal>().WithMany()
                .HasForeignKey(x => new { x.ReversesJournalId, x.BookId })
                .HasPrincipalKey(x => new { x.Id, x.BookId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountingJournalLine>(entity =>
        {
            entity.ToTable("accounting_journal_lines", table =>
            {
                table.HasCheckConstraint("ck_accounting_journal_lines_amount", "(debit_satang > 0 AND credit_satang = 0) OR (debit_satang = 0 AND credit_satang > 0)");
                table.HasCheckConstraint("ck_accounting_journal_lines_line_no", "line_no >= 1");
                table.HasCheckConstraint("ck_accounting_journal_lines_beneficiary", "beneficiary_slot_no IS NULL OR beneficiary_slot_no IN (1, 2)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasIndex(x => new { x.JournalId, x.LineNo }).IsUnique();
            entity.HasIndex(x => new { x.BookId, x.AccountId });
            entity.HasIndex(x => new { x.JournalId, x.BookId });
            entity.HasIndex(x => new { x.AccountId, x.BookId });
            entity.HasOne<AccountingJournal>().WithMany()
                .HasForeignKey(x => new { x.JournalId, x.BookId })
                .HasPrincipalKey(x => new { x.Id, x.BookId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountingAccount>().WithMany()
                .HasForeignKey(x => new { x.AccountId, x.BookId })
                .HasPrincipalKey(x => new { x.Id, x.BookId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountingBook>().WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountingPostingAudit>(entity =>
        {
            entity.ToTable("accounting_posting_audits", table =>
                table.HasCheckConstraint("ck_accounting_posting_audits_action", "length(trim(action)) > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Action).IsRequired();
            entity.Property(x => x.ActorUserId).IsRequired();
            entity.Property(x => x.ActorDisplayName).IsRequired();
            entity.Property(x => x.MachineName).IsRequired();
            entity.Property(x => x.AppVersion).IsRequired();
            entity.Property(x => x.DetailsSnapshot).IsRequired();
            entity.HasIndex(x => x.JournalId).IsUnique();
            entity.HasOne<AccountingJournal>().WithMany().HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ValidateAccountingWriteGuards()
    {
        RejectHistoryMutation<AccountingJournal>("รายการสมุดรายวันบัญชีที่บันทึกแล้วแก้ไขหรือลบไม่ได้");
        RejectHistoryMutation<AccountingJournalLine>("บรรทัดสมุดรายวันที่บันทึกแล้วแก้ไขหรือลบไม่ได้");
        RejectHistoryMutation<AccountingPostingAudit>("หลักฐานการผ่านรายการบัญชีแก้ไขหรือลบไม่ได้");
        RejectHistoryMutation<WelfareCollection>("ใบเรียกเก็บเงินสงเคราะห์แก้ไขหรือลบไม่ได้");

        var journals = ChangeTracker.Entries<AccountingJournal>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToArray();
        var lines = ChangeTracker.Entries<AccountingJournalLine>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToArray();
        var audits = ChangeTracker.Entries<AccountingPostingAudit>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToArray();

        if (journals.Length == 0)
        {
            if (lines.Length != 0 || audits.Length != 0)
                throw new InvalidOperationException("บรรทัดและหลักฐานบัญชีต้องบันทึกพร้อมหัวสมุดรายวันใหม่");
            return;
        }

        var journalIds = journals.Select(journal => journal.Id).ToHashSet();
        if (lines.Any(line => !journalIds.Contains(line.JournalId)) || audits.Any(audit => !journalIds.Contains(audit.JournalId)))
            throw new InvalidOperationException("บรรทัดและหลักฐานบัญชีต้องอ้างถึงหัวสมุดรายวันที่บันทึกพร้อมกัน");

        foreach (var journal in journals)
        {
            var journalLines = lines.Where(line => line.JournalId == journal.Id).ToArray();
            if (journalLines.Length < 2)
                throw new InvalidOperationException("สมุดรายวันต้องมีอย่างน้อยสองบรรทัด");
            if (journalLines.Any(line => line.BookId != journal.BookId))
                throw new InvalidOperationException("บรรทัดสมุดรายวันต้องอยู่ในสมุดบัญชีเดียวกับหัวรายการ");
            if (journalLines.Select(line => line.LineNo).Distinct().Count() != journalLines.Length)
                throw new InvalidOperationException("ลำดับบรรทัดสมุดรายวันต้องไม่ซ้ำกัน");

            long debit;
            long credit;
            try
            {
                debit = journalLines.Aggregate(0L, (total, line) => checked(total + line.DebitSatang));
                credit = journalLines.Aggregate(0L, (total, line) => checked(total + line.CreditSatang));
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException("ยอดเดบิตหรือเครดิตของสมุดรายวันเกินขอบเขตที่บันทึกได้", exception);
            }

            if (debit != credit)
                throw new InvalidOperationException("สมุดรายวันต้องมียอดเดบิตและเครดิตเท่ากัน");

            var journalAudits = audits.Where(audit => audit.JournalId == journal.Id).ToArray();
            if (journalAudits.Length != 1 || journalAudits[0].OperationId != journal.OperationId)
                throw new InvalidOperationException("สมุดรายวันต้องมีหลักฐานการผ่านรายการหนึ่งรายการที่อ้างถึงการดำเนินการเดียวกัน");
        }
    }

    private void RejectHistoryMutation<TEntity>(string message)
        where TEntity : class
    {
        if (ChangeTracker.Entries<TEntity>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException(message);
    }
}
