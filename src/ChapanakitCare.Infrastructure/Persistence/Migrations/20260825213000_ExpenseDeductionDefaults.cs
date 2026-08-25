using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapanakitCare.Infrastructure.Persistence.Migrations;

/// <summary>Moves only the untouched seeded defaults to the association's revised policy.</summary>
public partial class ExpenseDeductionDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE system_settings SET welfare_per_member_satang = 900, service_fee_rounding_mode = 'round_down_to_satang' WHERE welfare_per_member_satang = 1500 AND service_fee_rounding_mode = 'round_up_to_satang';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE system_settings SET welfare_per_member_satang = 1500, service_fee_rounding_mode = 'round_up_to_satang' WHERE welfare_per_member_satang = 900 AND service_fee_rounding_mode = 'round_down_to_satang';");
    }
}
