using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Web.Pages.Deaths;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class DeathRegistryPageTests
{
    [Fact]
    public void Death_registry_maps_two_beneficiaries_to_two_independent_columns()
    {
        var caseId = Guid.NewGuid();
        var beneficiaries = new[]
        {
            Beneficiary(caseId, 1, "หนึ่ง"),
            Beneficiary(caseId, 2, "สอง")
        };

        var columns = DeathRegistryBeneficiaryColumns.From(beneficiaries);

        Assert.Equal("นางหนึ่ง ทดสอบ", columns.First);
        Assert.Equal("นางสอง ทดสอบ", columns.Second);
    }

    [Fact]
    public async Task Death_registry_loads_its_own_persisted_column_order_visibility_and_sort()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var preferences = new TablePreferenceService(context);
        await preferences.SaveAsync("local-user", "death-library", ["caseNo", "memberName"], ["cause"], [], "caseNo", "asc", DateTimeOffset.UtcNow);
        var page = new IndexModel(context, new DeathApplicationService(context), preferences);

        await page.OnGetAsync();

        Assert.Equal(["caseNo", "memberName"], page.Preference.ColumnOrder);
        Assert.Equal(["cause"], page.Preference.HiddenColumns);
        Assert.Equal("caseNo", page.Preference.SortColumn);
        Assert.Equal("asc", page.Preference.SortDirection);
    }

    private static DeathBeneficiarySnapshot Beneficiary(Guid caseId, int slot, string firstName) => new()
    {
        Id = Guid.NewGuid(), DeathCaseId = caseId, SlotNo = slot, Title = "นาง", FirstName = firstName, LastName = "ทดสอบ"
    };
}
