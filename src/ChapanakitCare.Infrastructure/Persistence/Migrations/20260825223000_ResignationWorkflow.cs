using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations;

public partial class ResignationWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "ck_members_status", table: "members");
        migrationBuilder.AddCheckConstraint(name: "ck_members_status", table: "members", sql: "status IN ('normal', 'deceased', 'resigned')");
        migrationBuilder.DropCheckConstraint(name: "ck_member_status_events_from", table: "member_status_events");
        migrationBuilder.DropCheckConstraint(name: "ck_member_status_events_to", table: "member_status_events");
        migrationBuilder.AddCheckConstraint(name: "ck_member_status_events_from", table: "member_status_events", sql: "from_status IS NULL OR from_status IN ('normal', 'deceased', 'resigned')");
        migrationBuilder.AddCheckConstraint(name: "ck_member_status_events_to", table: "member_status_events", sql: "to_status IN ('normal', 'deceased', 'resigned')");
        migrationBuilder.DropCheckConstraint(name: "ck_advance_ledger_entries_type", table: "advance_ledger_entries");
        migrationBuilder.DropCheckConstraint(name: "ck_advance_ledger_entries_source", table: "advance_ledger_entries");
        migrationBuilder.AddCheckConstraint(name: "ck_advance_ledger_entries_type", table: "advance_ledger_entries", sql: "entry_type IN ('opening_30', 'death_contribution', 'reset_to_30', 'correction', 'resignation_refund')");
        migrationBuilder.AddCheckConstraint(name: "ck_advance_ledger_entries_source", table: "advance_ledger_entries", sql: "(entry_type = 'death_contribution' AND source_death_case_id IS NOT NULL AND source_reset_batch_id IS NULL) OR (entry_type = 'reset_to_30' AND source_reset_batch_id IS NOT NULL AND source_death_case_id IS NULL) OR (entry_type IN ('opening_30', 'correction', 'resignation_refund'))");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "ck_advance_ledger_entries_type", table: "advance_ledger_entries");
        migrationBuilder.DropCheckConstraint(name: "ck_advance_ledger_entries_source", table: "advance_ledger_entries");
        migrationBuilder.AddCheckConstraint(name: "ck_advance_ledger_entries_type", table: "advance_ledger_entries", sql: "entry_type IN ('opening_30', 'death_contribution', 'reset_to_30', 'correction')");
        migrationBuilder.AddCheckConstraint(name: "ck_advance_ledger_entries_source", table: "advance_ledger_entries", sql: "(entry_type = 'death_contribution' AND source_death_case_id IS NOT NULL AND source_reset_batch_id IS NULL) OR (entry_type = 'reset_to_30' AND source_reset_batch_id IS NOT NULL AND source_death_case_id IS NULL) OR (entry_type IN ('opening_30', 'correction'))");
        migrationBuilder.DropCheckConstraint(name: "ck_member_status_events_from", table: "member_status_events");
        migrationBuilder.DropCheckConstraint(name: "ck_member_status_events_to", table: "member_status_events");
        migrationBuilder.AddCheckConstraint(name: "ck_member_status_events_from", table: "member_status_events", sql: "from_status IS NULL OR from_status IN ('normal', 'deceased')");
        migrationBuilder.AddCheckConstraint(name: "ck_member_status_events_to", table: "member_status_events", sql: "to_status IN ('normal', 'deceased')");
        migrationBuilder.DropCheckConstraint(name: "ck_members_status", table: "members");
        migrationBuilder.AddCheckConstraint(name: "ck_members_status", table: "members", sql: "status IN ('normal', 'deceased')");
    }
}
