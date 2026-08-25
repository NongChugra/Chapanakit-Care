using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckpointFiveDeathCertificatesAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "visible_components_json",
                table: "ui_table_preferences",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "death_certificate_content_type",
                table: "death_cases",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "death_certificate_date",
                table: "death_cases",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "death_certificate_file_name",
                table: "death_cases",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "death_certificate_pdf",
                table: "death_cases",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "death_certificate_sha256",
                table: "death_cases",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "death_certificate_size",
                table: "death_cases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_death_cases_certificate_pdf",
                table: "death_cases",
                sql: "death_certificate_size BETWEEN 1 AND 10485760 AND length(death_certificate_pdf) = death_certificate_size AND length(death_certificate_sha256) = 64");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_death_cases_certificate_pdf",
                table: "death_cases");

            migrationBuilder.DropColumn(
                name: "visible_components_json",
                table: "ui_table_preferences");

            migrationBuilder.DropColumn(
                name: "death_certificate_content_type",
                table: "death_cases");

            migrationBuilder.DropColumn(
                name: "death_certificate_date",
                table: "death_cases");

            migrationBuilder.DropColumn(
                name: "death_certificate_file_name",
                table: "death_cases");

            migrationBuilder.DropColumn(
                name: "death_certificate_pdf",
                table: "death_cases");

            migrationBuilder.DropColumn(
                name: "death_certificate_sha256",
                table: "death_cases");

            migrationBuilder.DropColumn(
                name: "death_certificate_size",
                table: "death_cases");
        }
    }
}
