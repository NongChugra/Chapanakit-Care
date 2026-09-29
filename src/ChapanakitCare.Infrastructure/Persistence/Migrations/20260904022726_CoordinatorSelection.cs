using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoordinatorSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coordinator_positions",
                columns: table => new
                {
                    position_key = table.Column<string>(type: "TEXT", nullable: false),
                    role_code = table.Column<string>(type: "TEXT", nullable: false),
                    group_no = table.Column<string>(type: "TEXT", nullable: true),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    appointed_on = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coordinator_positions", x => x.position_key);
                    table.CheckConstraint("ck_coordinator_position_holder", "(member_id IS NULL AND appointed_on IS NULL) OR (member_id IS NOT NULL AND appointed_on IS NOT NULL)");
                    table.CheckConstraint("ck_coordinator_position_scope", "(role_code = 'chairperson' AND group_no IS NULL AND position_key = 'chairperson') OR (role_code = 'group_leader' AND group_no IS NOT NULL AND length(trim(group_no)) > 0 AND group_no = trim(group_no) AND position_key = 'group:' || group_no)");
                    table.CheckConstraint("ck_coordinator_position_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_coordinator_positions_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coordinator_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    position_key = table.Column<string>(type: "TEXT", nullable: false),
                    role_code = table.Column<string>(type: "TEXT", nullable: false),
                    group_no = table.Column<string>(type: "TEXT", nullable: true),
                    action = table.Column<string>(type: "TEXT", nullable: false),
                    previous_member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    previous_member_name = table.Column<string>(type: "TEXT", nullable: true),
                    previous_run_no = table.Column<string>(type: "TEXT", nullable: true),
                    member_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    member_name = table.Column<string>(type: "TEXT", nullable: true),
                    member_run_no = table.Column<string>(type: "TEXT", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    occurred_at_utc = table.Column<long>(type: "INTEGER", nullable: false),
                    actor = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    position_version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coordinator_events", x => x.id);
                    table.CheckConstraint("ck_coordinator_event_action", "(action = 'appoint' AND previous_member_id IS NULL AND member_id IS NOT NULL) OR (action = 'replace' AND previous_member_id IS NOT NULL AND member_id IS NOT NULL AND previous_member_id <> member_id) OR (action = 'end' AND previous_member_id IS NOT NULL AND member_id IS NULL)");
                    table.CheckConstraint("ck_coordinator_event_reason", "action = 'appoint' OR (reason IS NOT NULL AND length(trim(reason)) > 0)");
                    table.CheckConstraint("ck_coordinator_event_version", "position_version >= 1");
                    table.ForeignKey(
                        name: "FK_coordinator_events_coordinator_positions_position_key",
                        column: x => x.position_key,
                        principalTable: "coordinator_positions",
                        principalColumn: "position_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_coordinator_events_position_key_position_version",
                table: "coordinator_events",
                columns: new[] { "position_key", "position_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coordinator_positions_member_id",
                table: "coordinator_positions",
                column: "member_id",
                unique: true,
                filter: "member_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coordinator_events");

            migrationBuilder.DropTable(
                name: "coordinator_positions");
        }
    }
}
