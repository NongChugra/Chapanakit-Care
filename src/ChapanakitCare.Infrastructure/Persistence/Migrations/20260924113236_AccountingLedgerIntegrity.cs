using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingLedgerIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    journal_prefix = table.Column<string>(type: "TEXT", nullable: false),
                    is_activated = table.Column<bool>(type: "INTEGER", nullable: false),
                    cutover_start_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    opening_evidence = table.Column<string>(type: "TEXT", nullable: true),
                    activated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    activated_by = table.Column<string>(type: "TEXT", nullable: true),
                    next_journal_number = table.Column<long>(type: "INTEGER", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_books", x => x.id);
                    table.CheckConstraint("ck_accounting_books_activation", "is_activated IN (0, 1)");
                    table.CheckConstraint("ck_accounting_books_next_number", "next_journal_number >= 1");
                    table.CheckConstraint("ck_accounting_books_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "welfare_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    member_run_no = table.Column<string>(type: "TEXT", nullable: false),
                    member_name = table.Column<string>(type: "TEXT", nullable: false),
                    group_no = table.Column<string>(type: "TEXT", nullable: false),
                    cycle_key = table.Column<string>(type: "TEXT", nullable: false),
                    business_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    due_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    amount_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    request_token = table.Column<string>(type: "TEXT", nullable: false),
                    request_fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<long>(type: "INTEGER", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_welfare_collections", x => x.id);
                    table.CheckConstraint("ck_welfare_collection_amount", "amount_satang > 0");
                    table.CheckConstraint("ck_welfare_collection_cycle", "length(trim(cycle_key)) > 0");
                    table.CheckConstraint("ck_welfare_collection_dates", "due_date >= business_date");
                    table.ForeignKey(
                        name: "FK_welfare_collections_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    book_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    account_type = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    normal_balance = table.Column<string>(type: "TEXT", nullable: false),
                    is_bank_account = table.Column<bool>(type: "INTEGER", nullable: false),
                    bank_display_name = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_accounts", x => x.id);
                    table.UniqueConstraint("AK_accounting_accounts_id_book_id", x => new { x.id, x.book_id });
                    table.CheckConstraint("ck_accounting_accounts_bank", "is_bank_account IN (0, 1)");
                    table.CheckConstraint("ck_accounting_accounts_code", "length(trim(code)) > 0 AND code = trim(code)");
                    table.CheckConstraint("ck_accounting_accounts_normal_balance", "normal_balance IN ('Debit', 'Credit')");
                    table.CheckConstraint("ck_accounting_accounts_type", "account_type IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense')");
                    table.ForeignKey(
                        name: "FK_accounting_accounts_accounting_books_book_id",
                        column: x => x.book_id,
                        principalTable: "accounting_books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_journals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    book_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    operation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    linked_operation_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    journal_number = table.Column<string>(type: "TEXT", nullable: false),
                    voucher_type = table.Column<string>(type: "TEXT", nullable: false),
                    description_snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    business_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    request_token = table.Column<string>(type: "TEXT", nullable: false),
                    request_fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    reverses_journal_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    source_type = table.Column<string>(type: "TEXT", nullable: true),
                    source_id = table.Column<string>(type: "TEXT", nullable: true),
                    source_snapshot = table.Column<string>(type: "TEXT", nullable: true),
                    actor_user_id = table.Column<string>(type: "TEXT", nullable: false),
                    actor_display_name = table.Column<string>(type: "TEXT", nullable: false),
                    machine_name = table.Column<string>(type: "TEXT", nullable: false),
                    app_version = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_journals", x => x.id);
                    table.UniqueConstraint("AK_accounting_journals_id_book_id", x => new { x.id, x.book_id });
                    table.CheckConstraint("ck_accounting_journals_description", "length(trim(description_snapshot)) > 0");
                    table.CheckConstraint("ck_accounting_journals_fingerprint", "length(trim(request_fingerprint)) > 0");
                    table.CheckConstraint("ck_accounting_journals_number", "length(trim(journal_number)) > 0 AND journal_number = trim(journal_number)");
                    table.CheckConstraint("ck_accounting_journals_reversal", "reverses_journal_id IS NULL OR reverses_journal_id <> id");
                    table.CheckConstraint("ck_accounting_journals_token", "length(trim(request_token)) > 0 AND request_token = trim(request_token)");
                    table.CheckConstraint("ck_accounting_journals_voucher", "length(trim(voucher_type)) > 0");
                    table.ForeignKey(
                        name: "FK_accounting_journals_accounting_books_book_id",
                        column: x => x.book_id,
                        principalTable: "accounting_books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_journals_accounting_journals_reverses_journal_id_book_id",
                        columns: x => new { x.reverses_journal_id, x.book_id },
                        principalTable: "accounting_journals",
                        principalColumns: new[] { "id", "book_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    book_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    closed_by = table.Column<string>(type: "TEXT", nullable: true),
                    closure_evidence = table.Column<string>(type: "TEXT", nullable: true),
                    reopened_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    reopened_by = table.Column<string>(type: "TEXT", nullable: true),
                    reopen_reason = table.Column<string>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.id);
                    table.CheckConstraint("ck_accounting_periods_dates", "ends_on >= starts_on");
                    table.CheckConstraint("ck_accounting_periods_status", "status IN ('Open', 'Closed')");
                    table.CheckConstraint("ck_accounting_periods_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_accounting_periods_accounting_books_book_id",
                        column: x => x.book_id,
                        principalTable: "accounting_books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_journal_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    journal_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    book_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    account_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    line_no = table.Column<int>(type: "INTEGER", nullable: false),
                    debit_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    credit_satang = table.Column<long>(type: "INTEGER", nullable: false),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    death_case_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    beneficiary_slot_no = table.Column<int>(type: "INTEGER", nullable: true),
                    collection_request_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    party_snapshot = table.Column<string>(type: "TEXT", nullable: true),
                    description_snapshot = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_journal_lines", x => x.id);
                    table.CheckConstraint("ck_accounting_journal_lines_amount", "(debit_satang > 0 AND credit_satang = 0) OR (debit_satang = 0 AND credit_satang > 0)");
                    table.CheckConstraint("ck_accounting_journal_lines_beneficiary", "beneficiary_slot_no IS NULL OR beneficiary_slot_no IN (1, 2)");
                    table.CheckConstraint("ck_accounting_journal_lines_line_no", "line_no >= 1");
                    table.ForeignKey(
                        name: "FK_accounting_journal_lines_accounting_accounts_account_id_book_id",
                        columns: x => new { x.account_id, x.book_id },
                        principalTable: "accounting_accounts",
                        principalColumns: new[] { "id", "book_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_journal_lines_accounting_books_book_id",
                        column: x => x.book_id,
                        principalTable: "accounting_books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_journal_lines_accounting_journals_journal_id_book_id",
                        columns: x => new { x.journal_id, x.book_id },
                        principalTable: "accounting_journals",
                        principalColumns: new[] { "id", "book_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounting_posting_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    journal_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    operation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<string>(type: "TEXT", nullable: false),
                    actor_display_name = table.Column<string>(type: "TEXT", nullable: false),
                    machine_name = table.Column<string>(type: "TEXT", nullable: false),
                    app_version = table.Column<string>(type: "TEXT", nullable: false),
                    details_snapshot = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_posting_audits", x => x.id);
                    table.CheckConstraint("ck_accounting_posting_audits_action", "length(trim(action)) > 0");
                    table.ForeignKey(
                        name: "FK_accounting_posting_audits_accounting_journals_journal_id",
                        column: x => x.journal_id,
                        principalTable: "accounting_journals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_accounts_book_id_code",
                table: "accounting_accounts",
                columns: new[] { "book_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_books_code",
                table: "accounting_books",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journal_lines_account_id_book_id",
                table: "accounting_journal_lines",
                columns: new[] { "account_id", "book_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journal_lines_book_id_account_id",
                table: "accounting_journal_lines",
                columns: new[] { "book_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journal_lines_journal_id_book_id",
                table: "accounting_journal_lines",
                columns: new[] { "journal_id", "book_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journal_lines_journal_id_line_no",
                table: "accounting_journal_lines",
                columns: new[] { "journal_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journals_book_id_business_date",
                table: "accounting_journals",
                columns: new[] { "book_id", "business_date" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journals_book_id_journal_number",
                table: "accounting_journals",
                columns: new[] { "book_id", "journal_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journals_book_id_request_token",
                table: "accounting_journals",
                columns: new[] { "book_id", "request_token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journals_reverses_journal_id",
                table: "accounting_journals",
                column: "reverses_journal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_journals_reverses_journal_id_book_id",
                table: "accounting_journals",
                columns: new[] { "reverses_journal_id", "book_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_book_id_starts_on_ends_on",
                table: "accounting_periods",
                columns: new[] { "book_id", "starts_on", "ends_on" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_periods_book_id_status_starts_on_ends_on",
                table: "accounting_periods",
                columns: new[] { "book_id", "status", "starts_on", "ends_on" });

            migrationBuilder.CreateIndex(
                name: "IX_accounting_posting_audits_journal_id",
                table: "accounting_posting_audits",
                column: "journal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_welfare_collections_member_id_cycle_key",
                table: "welfare_collections",
                columns: new[] { "member_id", "cycle_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_welfare_collections_member_id_request_token",
                table: "welfare_collections",
                columns: new[] { "member_id", "request_token" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_journals_no_update
                BEFORE UPDATE ON accounting_journals
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting journals are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_journals_no_delete
                BEFORE DELETE ON accounting_journals
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting journals are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_journal_lines_no_update
                BEFORE UPDATE ON accounting_journal_lines
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting journal lines are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_journal_lines_no_delete
                BEFORE DELETE ON accounting_journal_lines
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting journal lines are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_posting_audits_no_update
                BEFORE UPDATE ON accounting_posting_audits
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting posting audits are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_posting_audits_no_delete
                BEFORE DELETE ON accounting_posting_audits
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting posting audits are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_welfare_collections_no_update
                BEFORE UPDATE ON welfare_collections
                BEGIN
                    SELECT RAISE(ABORT, 'Welfare collections are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_welfare_collections_no_delete
                BEFORE DELETE ON welfare_collections
                BEGIN
                    SELECT RAISE(ABORT, 'Welfare collections are append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_audit_events_no_update
                BEFORE UPDATE ON audit_events
                WHEN OLD.action LIKE 'accounting.%'
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting audit evidence is append-only.');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_accounting_audit_events_no_delete
                BEFORE DELETE ON audit_events
                WHEN OLD.action LIKE 'accounting.%'
                BEGIN
                    SELECT RAISE(ABORT, 'Accounting audit evidence is append-only.');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_audit_events_no_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_audit_events_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_welfare_collections_no_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_welfare_collections_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_posting_audits_no_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_posting_audits_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_journal_lines_no_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_journal_lines_no_update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_journals_no_delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_accounting_journals_no_update;");

            migrationBuilder.DropTable(
                name: "accounting_journal_lines");

            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "accounting_posting_audits");

            migrationBuilder.DropTable(
                name: "welfare_collections");

            migrationBuilder.DropTable(
                name: "accounting_accounts");

            migrationBuilder.DropTable(
                name: "accounting_journals");

            migrationBuilder.DropTable(
                name: "accounting_books");
        }
    }
}
