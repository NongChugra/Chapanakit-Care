using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFirstDaySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "advance_reset_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reset_no = table.Column<string>(type: "TEXT", nullable: false),
                    trigger_type = table.Column<string>(type: "TEXT", nullable: false),
                    target_units = table.Column<int>(type: "INTEGER", nullable: false),
                    deaths_since_previous_reset = table.Column<int>(type: "INTEGER", nullable: false),
                    previous_reset_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    idempotency_key = table.Column<string>(type: "TEXT", nullable: false),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    confirmed_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_reset_batches", x => x.id);
                    table.CheckConstraint("ck_advance_reset_batches_trigger", "trigger_type IN ('manual', 'month_start', 'death_threshold')");
                    table.CheckConstraint("ck_advance_reset_batches_values", "target_units >= 0 AND deaths_since_previous_reset >= 0");
                    table.ForeignKey(
                        name: "FK_advance_reset_batches_advance_reset_batches_previous_reset_id",
                        column: x => x.previous_reset_id,
                        principalTable: "advance_reset_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "backup_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false),
                    file_name = table.Column<string>(type: "TEXT", nullable: false),
                    file_size = table.Column<long>(type: "INTEGER", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    is_automatic = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_runs", x => x.id);
                    table.CheckConstraint("ck_backup_runs_automatic", "is_automatic IN (0, 1)");
                    table.CheckConstraint("ck_backup_runs_sha", "length(sha256) = 64");
                    table.CheckConstraint("ck_backup_runs_size", "file_size >= 0");
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    run_no = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: true),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    gender = table.Column<string>(type: "TEXT", nullable: true),
                    personal_id_card = table.Column<string>(type: "TEXT", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    house_no = table.Column<string>(type: "TEXT", nullable: true),
                    under = table.Column<string>(type: "TEXT", nullable: true),
                    moo = table.Column<string>(type: "TEXT", nullable: true),
                    subdistrict = table.Column<string>(type: "TEXT", nullable: true),
                    district = table.Column<string>(type: "TEXT", nullable: false),
                    province = table.Column<string>(type: "TEXT", nullable: false),
                    postal_code = table.Column<string>(type: "TEXT", nullable: true),
                    mobile = table.Column<string>(type: "TEXT", nullable: true),
                    group_no = table.Column<string>(type: "TEXT", nullable: true),
                    application_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    approval_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    coverage_start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    advance_units_balance = table.Column<int>(type: "INTEGER", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by = table.Column<string>(type: "TEXT", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    archived_by = table.Column<string>(type: "TEXT", nullable: true),
                    archive_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.id);
                    table.CheckConstraint("ck_members_archive", "archived_at_utc IS NULL OR archive_reason IS NOT NULL");
                    table.CheckConstraint("ck_members_postal_code", "postal_code IS NULL OR length(postal_code) = 5");
                    table.CheckConstraint("ck_members_run_no_length", "length(run_no) = 5");
                    table.CheckConstraint("ck_members_status", "status IN ('normal', 'deceased')");
                    table.CheckConstraint("ck_members_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "number_sequences",
                columns: table => new
                {
                    sequence_key = table.Column<string>(type: "TEXT", nullable: false),
                    next_value = table.Column<long>(type: "INTEGER", nullable: false),
                    width = table.Column<int>(type: "INTEGER", nullable: false),
                    prefix = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_number_sequences", x => x.sequence_key);
                    table.CheckConstraint("ck_number_sequences_next", "next_value >= 0");
                    table.CheckConstraint("ck_number_sequences_width", "width > 0");
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    settings_revision = table.Column<int>(type: "INTEGER", nullable: false),
                    registration_fee_satang = table.Column<long>(type: "INTEGER", nullable: true),
                    service_fee_basis_points = table.Column<int>(type: "INTEGER", nullable: false),
                    welfare_per_member_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    reset_target_units = table.Column<int>(type: "INTEGER", nullable: false),
                    coverage_wait_days = table.Column<int>(type: "INTEGER", nullable: false),
                    special_nonpay_window_days = table.Column<int>(type: "INTEGER", nullable: false),
                    death_warning_threshold = table.Column<int>(type: "INTEGER", nullable: false),
                    service_fee_rounding_mode = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                    table.CheckConstraint("ck_system_settings_fee", "service_fee_basis_points BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_system_settings_revision", "settings_revision >= 1");
                    table.CheckConstraint("ck_system_settings_singleton", "id = 1");
                    table.CheckConstraint("ck_system_settings_values", "welfare_per_member_satang > 0 AND reset_target_units >= 0 AND coverage_wait_days >= 0 AND special_nonpay_window_days >= 0 AND death_warning_threshold > 0");
                });

            migrationBuilder.CreateTable(
                name: "thai_address_reference",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    subdistrict = table.Column<string>(type: "TEXT", nullable: false),
                    district = table.Column<string>(type: "TEXT", nullable: false),
                    province = table.Column<string>(type: "TEXT", nullable: false),
                    postal_code = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    normalized_search = table.Column<string>(type: "TEXT", nullable: false),
                    source_name = table.Column<string>(type: "TEXT", nullable: false),
                    source_version = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_thai_address_reference", x => x.id);
                    table.CheckConstraint("ck_thai_address_reference_active", "is_active IN (0, 1)");
                    table.CheckConstraint("ck_thai_address_reference_postal", "length(postal_code) = 5");
                });

            migrationBuilder.CreateTable(
                name: "ui_table_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    profile_key = table.Column<string>(type: "TEXT", nullable: false),
                    table_key = table.Column<string>(type: "TEXT", nullable: false),
                    column_order_json = table.Column<string>(type: "TEXT", nullable: false),
                    hidden_columns_json = table.Column<string>(type: "TEXT", nullable: false),
                    column_widths_json = table.Column<string>(type: "TEXT", nullable: true),
                    sort_json = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_table_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    notification_type = table.Column<string>(type: "TEXT", nullable: false),
                    cycle_key = table.Column<string>(type: "TEXT", nullable: false),
                    triggered_business_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    triggered_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deaths_since_latest_reset = table.Column<int>(type: "INTEGER", nullable: true),
                    latest_reset_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    acknowledged_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    acknowledged_by = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_ack", "state <> 'acknowledged' OR acknowledged_at_utc IS NOT NULL");
                    table.CheckConstraint("ck_notifications_state", "state IN ('active', 'acknowledged')");
                    table.CheckConstraint("ck_notifications_type", "notification_type IN ('month_start_reset', 'death_threshold')");
                    table.ForeignKey(
                        name: "FK_notifications_advance_reset_batches_latest_reset_id",
                        column: x => x.latest_reset_id,
                        principalTable: "advance_reset_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    operation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<string>(type: "TEXT", nullable: false),
                    actor_display_name = table.Column<string>(type: "TEXT", nullable: false),
                    machine_name = table.Column<string>(type: "TEXT", nullable: false),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    app_version = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_events_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "death_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    death_case_no = table.Column<string>(type: "TEXT", nullable: false),
                    death_sequence_no = table.Column<long>(type: "INTEGER", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    recorded_business_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    death_certificate_no = table.Column<string>(type: "TEXT", nullable: false),
                    cause_of_death_text = table.Column<string>(type: "TEXT", nullable: false),
                    is_manual_nonpay_case = table.Column<bool>(type: "INTEGER", nullable: false),
                    manual_nonpay_reason = table.Column<string>(type: "TEXT", nullable: true),
                    eligibility_result = table.Column<string>(type: "TEXT", nullable: false),
                    days_since_coverage = table.Column<int>(type: "INTEGER", nullable: false),
                    settings_revision = table.Column<int>(type: "INTEGER", nullable: false),
                    record_state = table.Column<string>(type: "TEXT", nullable: false),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    confirmed_by = table.Column<string>(type: "TEXT", nullable: false),
                    voided_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    voided_by = table.Column<string>(type: "TEXT", nullable: true),
                    void_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_death_cases", x => x.id);
                    table.CheckConstraint("ck_death_cases_eligibility", "eligibility_result IN ('before_coverage_zero', 'payable', 'manual_nonpay_zero')");
                    table.CheckConstraint("ck_death_cases_manual", "is_manual_nonpay_case IN (0, 1)");
                    table.CheckConstraint("ck_death_cases_reason", "is_manual_nonpay_case = 0 OR manual_nonpay_reason IS NOT NULL");
                    table.CheckConstraint("ck_death_cases_state", "record_state IN ('confirmed', 'voided')");
                    table.CheckConstraint("ck_death_cases_void", "record_state <> 'voided' OR (voided_at_utc IS NOT NULL AND void_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_death_cases_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_beneficiaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    slot_no = table.Column<int>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: true),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    relationship = table.Column<string>(type: "TEXT", nullable: true),
                    personal_id_card = table.Column<string>(type: "TEXT", nullable: true),
                    mobile = table.Column<string>(type: "TEXT", nullable: true),
                    house_no = table.Column<string>(type: "TEXT", nullable: true),
                    under = table.Column<string>(type: "TEXT", nullable: true),
                    moo = table.Column<string>(type: "TEXT", nullable: true),
                    subdistrict = table.Column<string>(type: "TEXT", nullable: true),
                    district = table.Column<string>(type: "TEXT", nullable: true),
                    province = table.Column<string>(type: "TEXT", nullable: true),
                    postal_code = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by = table.Column<string>(type: "TEXT", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    archived_by = table.Column<string>(type: "TEXT", nullable: true),
                    archive_reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_beneficiaries", x => x.id);
                    table.CheckConstraint("ck_member_beneficiaries_active", "is_active IN (0, 1)");
                    table.CheckConstraint("ck_member_beneficiaries_archive", "archived_at_utc IS NULL OR archive_reason IS NOT NULL");
                    table.CheckConstraint("ck_member_beneficiaries_postal", "postal_code IS NULL OR length(postal_code) = 5");
                    table.CheckConstraint("ck_member_beneficiaries_slot", "slot_no IN (1, 2)");
                    table.CheckConstraint("ck_member_beneficiaries_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_member_beneficiaries_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_status_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    from_status = table.Column<string>(type: "TEXT", nullable: true),
                    to_status = table.Column<string>(type: "TEXT", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    source_type = table.Column<string>(type: "TEXT", nullable: false),
                    source_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_status_events", x => x.id);
                    table.CheckConstraint("ck_member_status_events_from", "from_status IS NULL OR from_status IN ('normal', 'deceased')");
                    table.CheckConstraint("ck_member_status_events_to", "to_status IN ('normal', 'deceased')");
                    table.ForeignKey(
                        name: "FK_member_status_events_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_field_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    audit_event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    field_name = table.Column<string>(type: "TEXT", nullable: false),
                    old_value_display = table.Column<string>(type: "TEXT", nullable: true),
                    new_value_display = table.Column<string>(type: "TEXT", nullable: true),
                    is_sensitive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_field_changes", x => x.id);
                    table.CheckConstraint("ck_audit_field_changes_sensitive", "is_sensitive IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_audit_field_changes_audit_events_audit_event_id",
                        column: x => x.audit_event_id,
                        principalTable: "audit_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "advance_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entry_order = table.Column<long>(type: "INTEGER", nullable: false),
                    entry_type = table.Column<string>(type: "TEXT", nullable: false),
                    business_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    units_delta = table.Column<int>(type: "INTEGER", nullable: false),
                    balance_before = table.Column<int>(type: "INTEGER", nullable: false),
                    balance_after = table.Column<int>(type: "INTEGER", nullable: false),
                    source_death_case_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    source_reset_batch_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_advance_ledger_entries_arithmetic", "balance_before + units_delta = balance_after");
                    table.CheckConstraint("ck_advance_ledger_entries_source", "(entry_type = 'death_contribution' AND source_death_case_id IS NOT NULL AND source_reset_batch_id IS NULL) OR (entry_type = 'reset_to_30' AND source_reset_batch_id IS NOT NULL AND source_death_case_id IS NULL) OR (entry_type IN ('opening_30', 'correction'))");
                    table.CheckConstraint("ck_advance_ledger_entries_type", "entry_type IN ('opening_30', 'death_contribution', 'reset_to_30', 'correction')");
                    table.ForeignKey(
                        name: "FK_advance_ledger_entries_advance_reset_batches_source_reset_batch_id",
                        column: x => x.source_reset_batch_id,
                        principalTable: "advance_reset_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_advance_ledger_entries_death_cases_source_death_case_id",
                        column: x => x.source_death_case_id,
                        principalTable: "death_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_advance_ledger_entries_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "death_beneficiary_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    death_case_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    slot_no = table.Column<int>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: true),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    relationship = table.Column<string>(type: "TEXT", nullable: true),
                    personal_id_card = table.Column<string>(type: "TEXT", nullable: true),
                    mobile = table.Column<string>(type: "TEXT", nullable: true),
                    house_no = table.Column<string>(type: "TEXT", nullable: true),
                    moo = table.Column<string>(type: "TEXT", nullable: true),
                    subdistrict = table.Column<string>(type: "TEXT", nullable: true),
                    district = table.Column<string>(type: "TEXT", nullable: true),
                    province = table.Column<string>(type: "TEXT", nullable: true),
                    postal_code = table.Column<string>(type: "TEXT", nullable: true),
                    share_numerator = table.Column<int>(type: "INTEGER", nullable: false),
                    share_denominator = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_death_beneficiary_snapshots", x => x.id);
                    table.CheckConstraint("ck_death_beneficiary_snapshots_postal", "postal_code IS NULL OR length(postal_code) = 5");
                    table.CheckConstraint("ck_death_beneficiary_snapshots_share", "share_numerator = 1 AND share_denominator IN (1, 2)");
                    table.CheckConstraint("ck_death_beneficiary_snapshots_slot", "slot_no IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_death_beneficiary_snapshots_death_cases_death_case_id",
                        column: x => x.death_case_id,
                        principalTable: "death_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "death_calculations",
                columns: table => new
                {
                    death_case_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    is_payable = table.Column<bool>(type: "INTEGER", nullable: false),
                    contributor_count = table.Column<int>(type: "INTEGER", nullable: false),
                    welfare_per_member_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    gross_collection_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    service_fee_basis_points = table.Column<int>(type: "INTEGER", nullable: false),
                    service_fee_rounding_mode = table.Column<string>(type: "TEXT", nullable: false),
                    service_fee_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    net_collection_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    deceased_advance_units = table.Column<int>(type: "INTEGER", nullable: false),
                    deceased_advance_value_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    total_benefit_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    beneficiary_count = table.Column<int>(type: "INTEGER", nullable: false),
                    calculated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_death_calculations", x => x.death_case_id);
                    table.CheckConstraint("ck_death_calculations_arithmetic", "gross_collection_satang - service_fee_satang = net_collection_satang AND net_collection_satang + deceased_advance_value_satang = total_benefit_satang");
                    table.CheckConstraint("ck_death_calculations_counts", "contributor_count >= 0 AND beneficiary_count BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_death_calculations_fee", "service_fee_basis_points BETWEEN 0 AND 10000 AND service_fee_satang >= 0");
                    table.CheckConstraint("ck_death_calculations_payable", "is_payable IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_death_calculations_death_cases_death_case_id",
                        column: x => x.death_case_id,
                        principalTable: "death_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "death_member_snapshots",
                columns: table => new
                {
                    death_case_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    run_no = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: true),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    gender = table.Column<string>(type: "TEXT", nullable: true),
                    personal_id_card = table.Column<string>(type: "TEXT", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    age_at_death = table.Column<int>(type: "INTEGER", nullable: true),
                    house_no = table.Column<string>(type: "TEXT", nullable: true),
                    under = table.Column<string>(type: "TEXT", nullable: true),
                    moo = table.Column<string>(type: "TEXT", nullable: true),
                    subdistrict = table.Column<string>(type: "TEXT", nullable: true),
                    district = table.Column<string>(type: "TEXT", nullable: false),
                    province = table.Column<string>(type: "TEXT", nullable: false),
                    postal_code = table.Column<string>(type: "TEXT", nullable: true),
                    mobile = table.Column<string>(type: "TEXT", nullable: true),
                    group_no = table.Column<string>(type: "TEXT", nullable: true),
                    application_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    approval_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    coverage_start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    advance_units_before_death = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_death_member_snapshots", x => x.death_case_id);
                    table.CheckConstraint("ck_death_member_snapshots_run", "length(run_no) = 5");
                    table.ForeignKey(
                        name: "FK_death_member_snapshots_death_cases_death_case_id",
                        column: x => x.death_case_id,
                        principalTable: "death_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "advance_reset_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reset_batch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    balance_before = table.Column<int>(type: "INTEGER", nullable: false),
                    balance_after = table.Column<int>(type: "INTEGER", nullable: false),
                    units_delta = table.Column<int>(type: "INTEGER", nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_advance_reset_lines", x => x.id);
                    table.CheckConstraint("ck_advance_reset_lines_arithmetic", "balance_before + units_delta = balance_after");
                    table.ForeignKey(
                        name: "FK_advance_reset_lines_advance_ledger_entries_ledger_entry_id",
                        column: x => x.ledger_entry_id,
                        principalTable: "advance_ledger_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_advance_reset_lines_advance_reset_batches_reset_batch_id",
                        column: x => x.reset_batch_id,
                        principalTable: "advance_reset_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_advance_reset_lines_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "coverage_wait_days", "death_warning_threshold", "registration_fee_satang", "reset_target_units", "service_fee_basis_points", "service_fee_rounding_mode", "settings_revision", "special_nonpay_window_days", "updated_at_utc", "updated_by", "welfare_per_member_satang" },
                values: new object[] { 1, 180, 25, null, 30, 400, "round_up_to_satang", 1, 365, new DateTimeOffset(new DateTime(2026, 8, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system_seed", 1500L });

            migrationBuilder.CreateIndex(
                name: "IX_advance_ledger_entries_member_id_entry_order",
                table: "advance_ledger_entries",
                columns: new[] { "member_id", "entry_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_advance_ledger_entries_source_death_case_id",
                table: "advance_ledger_entries",
                column: "source_death_case_id");

            migrationBuilder.CreateIndex(
                name: "IX_advance_ledger_entries_source_reset_batch_id",
                table: "advance_ledger_entries",
                column: "source_reset_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_advance_reset_batches_idempotency_key",
                table: "advance_reset_batches",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_advance_reset_batches_previous_reset_id",
                table: "advance_reset_batches",
                column: "previous_reset_id");

            migrationBuilder.CreateIndex(
                name: "IX_advance_reset_batches_reset_no",
                table: "advance_reset_batches",
                column: "reset_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_advance_reset_lines_ledger_entry_id",
                table: "advance_reset_lines",
                column: "ledger_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_advance_reset_lines_member_id",
                table: "advance_reset_lines",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_advance_reset_lines_reset_batch_id_member_id",
                table: "advance_reset_lines",
                columns: new[] { "reset_batch_id", "member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_member_id",
                table: "audit_events",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_operation_id",
                table: "audit_events",
                column: "operation_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_field_changes_audit_event_id",
                table: "audit_field_changes",
                column: "audit_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_death_beneficiary_snapshots_death_case_id_slot_no",
                table: "death_beneficiary_snapshots",
                columns: new[] { "death_case_id", "slot_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_death_cases_death_case_no",
                table: "death_cases",
                column: "death_case_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_death_cases_death_sequence_no",
                table: "death_cases",
                column: "death_sequence_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_death_cases_member_id",
                table: "death_cases",
                column: "member_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_beneficiaries_member_id_slot_no",
                table: "member_beneficiaries",
                columns: new[] { "member_id", "slot_no" },
                unique: true,
                filter: "is_active = 1");

            migrationBuilder.CreateIndex(
                name: "IX_members_personal_id_card",
                table: "members",
                column: "personal_id_card",
                unique: true,
                filter: "personal_id_card IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_members_run_no",
                table: "members",
                column: "run_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_status_events_member_id",
                table: "member_status_events",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_latest_reset_id",
                table: "notifications",
                column: "latest_reset_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_notification_type_cycle_key",
                table: "notifications",
                columns: new[] { "notification_type", "cycle_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_thai_address_reference_subdistrict_postal_code",
                table: "thai_address_reference",
                columns: new[] { "subdistrict", "postal_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_table_preferences_profile_key_table_key",
                table: "ui_table_preferences",
                columns: new[] { "profile_key", "table_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advance_reset_lines");

            migrationBuilder.DropTable(
                name: "audit_field_changes");

            migrationBuilder.DropTable(
                name: "backup_runs");

            migrationBuilder.DropTable(
                name: "death_beneficiary_snapshots");

            migrationBuilder.DropTable(
                name: "death_calculations");

            migrationBuilder.DropTable(
                name: "death_member_snapshots");

            migrationBuilder.DropTable(
                name: "member_beneficiaries");

            migrationBuilder.DropTable(
                name: "member_status_events");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "number_sequences");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "thai_address_reference");

            migrationBuilder.DropTable(
                name: "ui_table_preferences");

            migrationBuilder.DropTable(
                name: "advance_ledger_entries");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "advance_reset_batches");

            migrationBuilder.DropTable(
                name: "death_cases");

            migrationBuilder.DropTable(
                name: "members");
        }
    }
}
