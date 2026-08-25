using System.Text;
using ChapanakitCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChapanakitCare.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberBeneficiary> MemberBeneficiaries => Set<MemberBeneficiary>();
    public DbSet<MemberStatusEvent> MemberStatusEvents => Set<MemberStatusEvent>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<DeathCase> DeathCases => Set<DeathCase>();
    public DbSet<DeathMemberSnapshot> DeathMemberSnapshots => Set<DeathMemberSnapshot>();
    public DbSet<DeathBeneficiarySnapshot> DeathBeneficiarySnapshots => Set<DeathBeneficiarySnapshot>();
    public DbSet<DeathCalculation> DeathCalculations => Set<DeathCalculation>();
    public DbSet<AdvanceLedgerEntry> AdvanceLedgerEntries => Set<AdvanceLedgerEntry>();
    public DbSet<AdvanceResetBatch> AdvanceResetBatches => Set<AdvanceResetBatch>();
    public DbSet<AdvanceResetLine> AdvanceResetLines => Set<AdvanceResetLine>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UiTablePreference> UiTablePreferences => Set<UiTablePreference>();
    public DbSet<ThaiAddressReference> ThaiAddressReference => Set<ThaiAddressReference>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AuditFieldChange> AuditFieldChanges => Set<AuditFieldChange>();
    public DbSet<BackupRun> BackupRuns => Set<BackupRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureMembers(modelBuilder);
        ConfigureSettingsAndSequences(modelBuilder);
        ConfigureDeath(modelBuilder);
        ConfigureAdvance(modelBuilder);
        ConfigureSupportingTables(modelBuilder);
        ApplySnakeCaseColumnNames(modelBuilder);
    }

    private static void ConfigureMembers(ModelBuilder modelBuilder)
    {
        var statusConverter = new ValueConverter<MemberStatus, string>(
            value => value == MemberStatus.Normal ? "normal" : value == MemberStatus.Deceased ? "deceased" : "resigned",
            value => value == "normal" ? MemberStatus.Normal : value == "deceased" ? MemberStatus.Deceased : MemberStatus.Resigned);

        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("members", table =>
            {
                table.HasCheckConstraint("ck_members_run_no_length", "length(run_no) = 5");
                table.HasCheckConstraint("ck_members_status", "status IN ('normal', 'deceased', 'resigned')");
                table.HasCheckConstraint("ck_members_version", "version >= 1");
                table.HasCheckConstraint("ck_members_postal_code", "postal_code IS NULL OR length(postal_code) = 5");
                table.HasCheckConstraint("ck_members_archive", "archived_at_utc IS NULL OR archive_reason IS NOT NULL");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.RunNo).HasMaxLength(5).IsRequired();
            entity.HasIndex(value => value.RunNo).IsUnique();
            entity.Property(value => value.FirstName).IsRequired();
            entity.Property(value => value.LastName).IsRequired();
            entity.Property(value => value.District).IsRequired();
            entity.Property(value => value.Province).IsRequired();
            entity.Property(value => value.ApplicationDate).IsRequired();
            entity.Property(value => value.ApprovalDate).IsRequired();
            entity.Property(value => value.CoverageStartDate).IsRequired();
            entity.Property(value => value.Status).HasConversion(statusConverter).IsRequired();
            entity.Property(value => value.Version).IsConcurrencyToken();
            entity.Property(value => value.CreatedBy).IsRequired();
            entity.Property(value => value.UpdatedBy).IsRequired();
            entity.HasIndex(value => value.PersonalIdCard)
                .IsUnique()
                .HasFilter("personal_id_card IS NOT NULL");
        });

        modelBuilder.Entity<MemberBeneficiary>(entity =>
        {
            entity.ToTable("member_beneficiaries", table =>
            {
                table.HasCheckConstraint("ck_member_beneficiaries_slot", "slot_no IN (1, 2)");
                table.HasCheckConstraint("ck_member_beneficiaries_active", "is_active IN (0, 1)");
                table.HasCheckConstraint("ck_member_beneficiaries_version", "version >= 1");
                table.HasCheckConstraint("ck_member_beneficiaries_postal", "postal_code IS NULL OR length(postal_code) = 5");
                table.HasCheckConstraint("ck_member_beneficiaries_archive", "archived_at_utc IS NULL OR archive_reason IS NOT NULL");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.FirstName).IsRequired();
            entity.Property(value => value.LastName).IsRequired();
            entity.Property(value => value.CreatedBy).IsRequired();
            entity.Property(value => value.UpdatedBy).IsRequired();
            entity.Property(value => value.Version).IsConcurrencyToken();
            entity.HasIndex(value => new { value.MemberId, value.SlotNo })
                .IsUnique()
                .HasFilter("is_active = 1");
            entity.HasOne<Member>().WithMany().HasForeignKey(value => value.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MemberStatusEvent>(entity =>
        {
            entity.ToTable("member_status_events", table =>
            {
                table.HasCheckConstraint("ck_member_status_events_from", "from_status IS NULL OR from_status IN ('normal', 'deceased', 'resigned')");
                table.HasCheckConstraint("ck_member_status_events_to", "to_status IN ('normal', 'deceased', 'resigned')");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.ToStatus).IsRequired();
            entity.Property(value => value.SourceType).IsRequired();
            entity.Property(value => value.CreatedBy).IsRequired();
            entity.HasOne<Member>().WithMany().HasForeignKey(value => value.MemberId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSettingsAndSequences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.ToTable("system_settings", table =>
            {
                table.HasCheckConstraint("ck_system_settings_singleton", "id = 1");
                table.HasCheckConstraint("ck_system_settings_revision", "settings_revision >= 1");
                table.HasCheckConstraint("ck_system_settings_fee", "service_fee_basis_points BETWEEN 0 AND 10000 AND (registration_fee_satang IS NULL OR registration_fee_satang >= 0)");
                table.HasCheckConstraint("ck_system_settings_values", "welfare_per_member_satang > 0 AND reset_target_units >= 0 AND coverage_wait_days >= 0 AND special_nonpay_window_days >= 0 AND death_warning_threshold > 0");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.ServiceFeeRoundingMode).IsRequired();
            entity.Property(value => value.UpdatedBy).IsRequired();
            entity.HasData(new SystemSettings
            {
                Id = 1,
                SettingsRevision = 1,
                RegistrationFeeSatang = null,
                ServiceFeeBasisPoints = 400,
                WelfarePerMemberSatang = 900,
                ResetTargetUnits = 30,
                CoverageWaitDays = 180,
                SpecialNonPayWindowDays = 365,
                DeathWarningThreshold = 25,
                ServiceFeeRoundingMode = "round_down_to_satang",
                UpdatedAtUtc = new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero),
                UpdatedBy = "system_seed"
            });
        });

        modelBuilder.Entity<NumberSequence>(entity =>
        {
            entity.ToTable("number_sequences", table =>
            {
                table.HasCheckConstraint("ck_number_sequences_next", "next_value >= 0");
                table.HasCheckConstraint("ck_number_sequences_width", "width > 0");
            });
            entity.HasKey(value => value.SequenceKey);
            entity.Property(value => value.SequenceKey).IsRequired();
        });
    }

    private static void ConfigureDeath(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeathCase>(entity =>
        {
            entity.ToTable("death_cases", table =>
            {
                table.HasCheckConstraint("ck_death_cases_manual", "is_manual_nonpay_case IN (0, 1)");
                table.HasCheckConstraint("ck_death_cases_reason", "is_manual_nonpay_case = 0 OR manual_nonpay_reason IS NOT NULL");
                table.HasCheckConstraint("ck_death_cases_eligibility", "eligibility_result IN ('before_coverage_zero', 'payable', 'manual_nonpay_zero')");
                table.HasCheckConstraint("ck_death_cases_state", "record_state IN ('confirmed', 'voided')");
                table.HasCheckConstraint("ck_death_cases_void", "record_state <> 'voided' OR (voided_at_utc IS NOT NULL AND void_reason IS NOT NULL)");
                table.HasCheckConstraint("ck_death_cases_certificate_pdf", "death_certificate_size BETWEEN 1 AND 10485760 AND length(death_certificate_pdf) = death_certificate_size AND length(death_certificate_sha256) = 64");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.DeathCaseNo).IsRequired();
            entity.Property(value => value.DeathCertificateNo).IsRequired();
            entity.Property(value => value.DeathCertificateFileName).IsRequired();
            entity.Property(value => value.DeathCertificateContentType).IsRequired();
            entity.Property(value => value.DeathCertificatePdf).IsRequired();
            entity.Property(value => value.DeathCertificateSha256).HasMaxLength(64).IsRequired();
            entity.Property(value => value.CauseOfDeathText).IsRequired();
            entity.Property(value => value.EligibilityResult).IsRequired();
            entity.Property(value => value.RecordState).IsRequired();
            entity.Property(value => value.ConfirmedBy).IsRequired();
            entity.HasIndex(value => value.DeathCaseNo).IsUnique();
            entity.HasIndex(value => value.DeathSequenceNo).IsUnique();
            entity.HasIndex(value => value.MemberId).IsUnique();
            entity.HasOne<Member>().WithMany().HasForeignKey(value => value.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeathMemberSnapshot>(entity =>
        {
            entity.ToTable("death_member_snapshots", table =>
                table.HasCheckConstraint("ck_death_member_snapshots_run", "length(run_no) = 5"));
            entity.HasKey(value => value.DeathCaseId);
            entity.Property(value => value.DeathCaseId).ValueGeneratedNever();
            entity.Property(value => value.RunNo).HasMaxLength(5).IsRequired();
            entity.Property(value => value.FirstName).IsRequired();
            entity.Property(value => value.LastName).IsRequired();
            entity.Property(value => value.District).IsRequired();
            entity.Property(value => value.Province).IsRequired();
            entity.HasOne<DeathCase>().WithOne().HasForeignKey<DeathMemberSnapshot>(value => value.DeathCaseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeathBeneficiarySnapshot>(entity =>
        {
            entity.ToTable("death_beneficiary_snapshots", table =>
            {
                table.HasCheckConstraint("ck_death_beneficiary_snapshots_slot", "slot_no IN (1, 2)");
                table.HasCheckConstraint("ck_death_beneficiary_snapshots_share", "share_numerator = 1 AND share_denominator IN (1, 2)");
                table.HasCheckConstraint("ck_death_beneficiary_snapshots_postal", "postal_code IS NULL OR length(postal_code) = 5");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.FirstName).IsRequired();
            entity.Property(value => value.LastName).IsRequired();
            entity.HasIndex(value => new { value.DeathCaseId, value.SlotNo }).IsUnique();
            entity.HasOne<DeathCase>().WithMany().HasForeignKey(value => value.DeathCaseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeathCalculation>(entity =>
        {
            entity.ToTable("death_calculations", table =>
            {
                table.HasCheckConstraint("ck_death_calculations_payable", "is_payable IN (0, 1)");
                table.HasCheckConstraint("ck_death_calculations_counts", "contributor_count >= 0 AND beneficiary_count BETWEEN 0 AND 2");
                table.HasCheckConstraint("ck_death_calculations_fee", "service_fee_basis_points BETWEEN 0 AND 10000 AND service_fee_satang >= 0");
                table.HasCheckConstraint("ck_death_calculations_arithmetic", "gross_collection_satang - service_fee_satang = net_collection_satang AND net_collection_satang + deceased_advance_value_satang = total_benefit_satang");
            });
            entity.HasKey(value => value.DeathCaseId);
            entity.Property(value => value.DeathCaseId).ValueGeneratedNever();
            entity.Property(value => value.ServiceFeeRoundingMode).IsRequired();
            entity.HasOne<DeathCase>().WithOne().HasForeignKey<DeathCalculation>(value => value.DeathCaseId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAdvance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdvanceLedgerEntry>(entity =>
        {
            entity.ToTable("advance_ledger_entries", table =>
            {
                table.HasCheckConstraint("ck_advance_ledger_entries_type", "entry_type IN ('opening_30', 'death_contribution', 'reset_to_30', 'correction', 'resignation_refund')");
                table.HasCheckConstraint("ck_advance_ledger_entries_arithmetic", "balance_before + units_delta = balance_after");
                table.HasCheckConstraint("ck_advance_ledger_entries_source", "(entry_type = 'death_contribution' AND source_death_case_id IS NOT NULL AND source_reset_batch_id IS NULL) OR (entry_type = 'reset_to_30' AND source_reset_batch_id IS NOT NULL AND source_death_case_id IS NULL) OR (entry_type IN ('opening_30', 'correction', 'resignation_refund'))");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.EntryType).IsRequired();
            entity.Property(value => value.CreatedBy).IsRequired();
            entity.HasIndex(value => new { value.MemberId, value.EntryOrder }).IsUnique();
            entity.HasOne<Member>().WithMany().HasForeignKey(value => value.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeathCase>().WithMany().HasForeignKey(value => value.SourceDeathCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AdvanceResetBatch>().WithMany().HasForeignKey(value => value.SourceResetBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdvanceResetBatch>(entity =>
        {
            entity.ToTable("advance_reset_batches", table =>
            {
                table.HasCheckConstraint("ck_advance_reset_batches_trigger", "trigger_type IN ('manual', 'month_start', 'death_threshold')");
                table.HasCheckConstraint("ck_advance_reset_batches_values", "target_units >= 0 AND deaths_since_previous_reset >= 0");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.ResetNo).IsRequired();
            entity.Property(value => value.TriggerType).IsRequired();
            entity.Property(value => value.IdempotencyKey).IsRequired();
            entity.Property(value => value.ConfirmedBy).IsRequired();
            entity.HasIndex(value => value.ResetNo).IsUnique();
            entity.HasIndex(value => value.IdempotencyKey).IsUnique();
            entity.HasOne<AdvanceResetBatch>().WithMany().HasForeignKey(value => value.PreviousResetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdvanceResetLine>(entity =>
        {
            entity.ToTable("advance_reset_lines", table =>
                table.HasCheckConstraint("ck_advance_reset_lines_arithmetic", "balance_before + units_delta = balance_after"));
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.HasIndex(value => new { value.ResetBatchId, value.MemberId }).IsUnique();
            entity.HasIndex(value => value.LedgerEntryId).IsUnique();
            entity.HasOne<AdvanceResetBatch>().WithMany().HasForeignKey(value => value.ResetBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Member>().WithMany().HasForeignKey(value => value.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AdvanceLedgerEntry>().WithOne().HasForeignKey<AdvanceResetLine>(value => value.LedgerEntryId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSupportingTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications", table =>
            {
                table.HasCheckConstraint("ck_notifications_type", "notification_type IN ('month_start_reset', 'death_threshold')");
                table.HasCheckConstraint("ck_notifications_state", "state IN ('active', 'acknowledged')");
                table.HasCheckConstraint("ck_notifications_ack", "state <> 'acknowledged' OR acknowledged_at_utc IS NOT NULL");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.NotificationType).IsRequired();
            entity.Property(value => value.CycleKey).IsRequired();
            entity.Property(value => value.Message).IsRequired();
            entity.Property(value => value.State).IsRequired();
            entity.HasIndex(value => new { value.NotificationType, value.CycleKey }).IsUnique();
            entity.HasOne<AdvanceResetBatch>().WithMany().HasForeignKey(value => value.LatestResetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UiTablePreference>(entity =>
        {
            entity.ToTable("ui_table_preferences");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.ProfileKey).IsRequired();
            entity.Property(value => value.TableKey).IsRequired();
            entity.Property(value => value.ColumnOrderJson).IsRequired();
            entity.Property(value => value.HiddenColumnsJson).IsRequired();
            entity.Property(value => value.VisibleComponentsJson).IsRequired();
            entity.HasIndex(value => new { value.ProfileKey, value.TableKey }).IsUnique();
        });

        modelBuilder.Entity<ThaiAddressReference>(entity =>
        {
            entity.ToTable("thai_address_reference", table =>
            {
                table.HasCheckConstraint("ck_thai_address_reference_active", "is_active IN (0, 1)");
                table.HasCheckConstraint("ck_thai_address_reference_postal", "length(postal_code) = 5");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedOnAdd();
            entity.Property(value => value.Subdistrict).IsRequired();
            entity.Property(value => value.District).IsRequired();
            entity.Property(value => value.Province).IsRequired();
            entity.Property(value => value.PostalCode).HasMaxLength(5).IsRequired();
            entity.Property(value => value.NormalizedSearch).IsRequired();
            entity.Property(value => value.SourceName).IsRequired();
            entity.HasIndex(value => new { value.Subdistrict, value.PostalCode }).IsUnique();
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.ActorUserId).IsRequired();
            entity.Property(value => value.ActorDisplayName).IsRequired();
            entity.Property(value => value.MachineName).IsRequired();
            entity.Property(value => value.Action).IsRequired();
            entity.Property(value => value.EntityType).IsRequired();
            entity.Property(value => value.EntityId).IsRequired();
            entity.Property(value => value.AppVersion).IsRequired();
            entity.HasIndex(value => value.OperationId);
            entity.HasIndex(value => value.MemberId);
            entity.HasOne<Member>().WithMany().HasForeignKey(value => value.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditFieldChange>(entity =>
        {
            entity.ToTable("audit_field_changes", table =>
                table.HasCheckConstraint("ck_audit_field_changes_sensitive", "is_sensitive IN (0, 1)"));
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.FieldName).IsRequired();
            entity.HasOne<AuditEvent>().WithMany().HasForeignKey(value => value.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BackupRun>(entity =>
        {
            entity.ToTable("backup_runs", table =>
            {
                table.HasCheckConstraint("ck_backup_runs_size", "file_size >= 0");
                table.HasCheckConstraint("ck_backup_runs_automatic", "is_automatic IN (0, 1)");
                table.HasCheckConstraint("ck_backup_runs_sha", "length(sha256) = 64");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).ValueGeneratedNever();
            entity.Property(value => value.CreatedBy).IsRequired();
            entity.Property(value => value.FileName).IsRequired();
            entity.Property(value => value.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(value => value.SchemaVersion).IsRequired();
        });
    }

    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(property.Name switch
                {
                    nameof(ChapanakitCare.Domain.Entities.SystemSettings.SpecialNonPayWindowDays) => "special_nonpay_window_days",
                    nameof(ChapanakitCare.Domain.Entities.DeathCase.IsManualNonPayCase) => "is_manual_nonpay_case",
                    nameof(ChapanakitCare.Domain.Entities.DeathCase.ManualNonPayReason) => "manual_nonpay_reason",
                    _ => ToSnakeCase(property.Name)
                });
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
