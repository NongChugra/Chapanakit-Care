using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class DatabaseContractTests
{
    private static readonly string[] ExpectedTables =
    [
        "advance_ledger_entries",
        "advance_reset_batches",
        "advance_reset_lines",
        "audit_events",
        "audit_field_changes",
        "backup_runs",
        "death_beneficiary_snapshots",
        "death_calculations",
        "death_cases",
        "death_member_snapshots",
        "member_beneficiaries",
        "member_status_events",
        "members",
        "notifications",
        "number_sequences",
        "system_settings",
        "thai_address_reference",
        "ui_table_preferences"
    ];

    [Fact]
    public async Task Ef_model_creates_every_first_day_table_in_real_sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);

        await db.Database.EnsureCreatedAsync();

        var tables = await ReadApplicationTables(connection);

        Assert.Equal(ExpectedTables, tables);
    }

    [Fact]
    public async Task Initial_migration_creates_the_first_day_schema_and_seed_settings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);

        await db.Database.MigrateAsync();

        var tables = await ReadApplicationTables(connection);
        Assert.Equal(ExpectedTables, tables);
        Assert.Equal(400, (await db.SystemSettings.SingleAsync()).ServiceFeeBasisPoints);
    }

    [Fact]
    public async Task Database_seeds_the_single_launcher_settings_row_with_agreed_defaults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var settings = await db.SystemSettings.SingleAsync();

        Assert.Equal(1, settings.Id);
        Assert.Null(settings.RegistrationFeeSatang);
        Assert.Equal(400, settings.ServiceFeeBasisPoints);
        Assert.Equal(1_500, settings.WelfarePerMemberSatang);
        Assert.Equal(30, settings.ResetTargetUnits);
        Assert.Equal(180, settings.CoverageWaitDays);
        Assert.Equal(365, settings.SpecialNonPayWindowDays);
        Assert.Equal(25, settings.DeathWarningThreshold);
        Assert.Equal("round_up_to_satang", settings.ServiceFeeRoundingMode);
    }

    [Fact]
    public async Task Database_rejects_a_third_beneficiary_slot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        db.Members.Add(TestData.Member());
        db.MemberBeneficiaries.Add(TestData.Beneficiary(slotNo: 3));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_rejects_member_run_number_that_is_not_exactly_five_characters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        var member = TestData.Member();
        member.RunNo = "1234";
        db.Members.Add(member);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging(false)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<List<string>> ReadApplicationTables(SqliteConnection connection)
    {
        var tables = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrations%' ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
