using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalReviewerClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_system_settings_fee",
                table: "system_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_system_settings_fee",
                table: "system_settings",
                sql: "service_fee_basis_points BETWEEN 0 AND 10000 AND (registration_fee_satang IS NULL OR registration_fee_satang >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_system_settings_fee",
                table: "system_settings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_system_settings_fee",
                table: "system_settings",
                sql: "service_fee_basis_points BETWEEN 0 AND 10000");
        }
    }
}
