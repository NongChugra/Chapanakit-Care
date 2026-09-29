using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Coordinators;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChapanakitCare.Domain.Tests;

public sealed class CoordinatorMigrationTests
{
    private static readonly DateOnly Today = new(2026, 9, 4);
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_model_has_no_unmigrated_changes()
    {
        using var db = CreateContext("Data Source=:memory:");
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Existing_database_upgrades_without_rewriting_members_and_backup_preserves_roles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "coordinator-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var db = CreateContext($"Data Source={Path.Combine(directory, "source.db")};Pooling=False");
            await db.GetService<IMigrator>().MigrateAsync("20260825193212_DemoReadyRoundingAndResignation");
            var member = TestData.Member(); member.GroupNo = "A";
            db.Members.Add(member);
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            var preserved = await db.Members.SingleAsync();
            Assert.Equal(member.Id, preserved.Id);
            Assert.Equal(member.RunNo, preserved.RunNo);
            Assert.Equal(member.AdvanceUnitsBalance, preserved.AdvanceUnitsBalance);
            Assert.Equal(member.CreatedAtUtc, preserved.CreatedAtUtc);
            await new CoordinatorApplicationService(db).ChangeAsync(new("group:A", "appoint", member.Id, 0, null, null), Today, Now, "tester");
            var backup = await new BackupApplicationService(db).CreateAsync(directory, Now, "tester");
            await using var restored = CreateContext($"Data Source={backup.FullPath};Pooling=False");
            Assert.False(restored.Database.HasPendingModelChanges());
            Assert.Equal(member.Id, (await restored.CoordinatorPositions.SingleAsync()).MemberId);
            Assert.Equal(member.RunNo, (await restored.CoordinatorEvents.SingleAsync()).MemberRunNo);
            Assert.Equal(30, (await restored.Members.SingleAsync()).AdvanceUnitsBalance);
            Assert.Equal(await db.Database.GetAppliedMigrationsAsync(), await restored.Database.GetAppliedMigrationsAsync());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Competing_requests_cannot_occupy_the_same_slot_or_assign_two_roles_to_one_member(bool differentSlots)
    {
        var path = Path.Combine(Path.GetTempPath(), "coordinator-race-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = $"Data Source={path};Pooling=False;Default Timeout=5";
        try
        {
            var first = TestData.Member(); first.GroupNo = "A";
            var second = TestData.Member(); second.Id = Guid.NewGuid(); second.RunNo = "00002"; second.GroupNo = "A";
            await using (var seed = CreateContext(connectionString))
            {
                await seed.Database.EnsureCreatedAsync(); seed.Members.AddRange(first, second); await seed.SaveChangesAsync();
            }
            async Task<bool> Assign(string key, Guid memberId)
            {
                await using var db = CreateContext(connectionString);
                try
                {
                    await new CoordinatorApplicationService(db).ChangeAsync(new(key, "appoint", memberId, 0, null, null), Today, Now, "tester");
                    return true;
                }
                catch (MemberValidationException) { return false; }
            }
            var results = await Task.WhenAll(Task.Run(() => Assign("chairperson", first.Id)),
                Task.Run(() => Assign(differentSlots ? "group:A" : "chairperson", differentSlots ? first.Id : second.Id)));
            Assert.Single(results, x => x);
            await using var verify = CreateContext(connectionString);
            Assert.Single(await verify.CoordinatorPositions.Where(x => x.MemberId != null).ToListAsync());
            Assert.Single(await verify.CoordinatorEvents.ToListAsync());
        }
        finally { File.Delete(path); }
    }

    private static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
}
