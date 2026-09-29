using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecipientPhotosAndDurableHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_events_members_member_id",
                table: "audit_events");

            migrationBuilder.AddColumn<DateOnly>(
                name: "reported_certificate_date",
                table: "death_cases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "death_recipient_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    death_case_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    beneficiary_slot_no = table.Column<int>(type: "INTEGER", nullable: false),
                    file_name = table.Column<string>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    bytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_by = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_death_recipient_photos", x => x.id);
                    table.CheckConstraint("ck_recipient_photo_bytes", "length(bytes) BETWEEN 1 AND 10485760 AND length(sha256) = 64");
                    table.CheckConstraint("ck_recipient_photo_slot", "beneficiary_slot_no IN (1, 2)");
                    table.CheckConstraint("ck_recipient_photo_type", "content_type IN ('image/png', 'image/jpeg')");
                    table.ForeignKey(
                        name: "FK_death_recipient_photos_death_cases_death_case_id",
                        column: x => x.death_case_id,
                        principalTable: "death_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_death_recipient_photos_death_case_id_beneficiary_slot_no",
                table: "death_recipient_photos",
                columns: new[] { "death_case_id", "beneficiary_slot_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "death_recipient_photos");

            migrationBuilder.DropColumn(
                name: "reported_certificate_date",
                table: "death_cases");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_events_members_member_id",
                table: "audit_events",
                column: "member_id",
                principalTable: "members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
