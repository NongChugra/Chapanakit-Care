using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class ResignationWorkflowTests
{
    [Fact]
    public async Task Fresh_migrated_database_accepts_the_resigned_status_and_refund_ledger_entry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.MigrateAsync();
        var member = new Member
        {
            Id = Guid.NewGuid(), RunNo = "00001", FirstName = "มาลี", LastName = "ทดสอบ", District = "ร้องกวาง", Province = "แพร่",
            ApplicationDate = new DateOnly(2025, 1, 1), ApprovalDate = new DateOnly(2025, 1, 1), CoverageStartDate = new DateOnly(2025, 7, 1),
            AdvanceUnitsBalance = 7, Status = MemberStatus.Normal, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "test", UpdatedBy = "test"
        };
        context.Members.Add(member);
        await context.SaveChangesAsync();

        var result = await new ResignationApplicationService(context).ConfirmAsync(
            member.RunNo, new DateOnly(2026, 8, 26), DateTimeOffset.UtcNow, "tester");

        Assert.Equal(MemberStatus.Resigned, result.Member.Status);
        Assert.Equal("resignation_refund", (await context.AdvanceLedgerEntries.SingleAsync()).EntryType);
    }

    [Fact]
    public async Task Confirming_a_resignation_refunds_the_remaining_advance_and_cannot_be_repeated()
    {
        await using var db = await TestDatabase.CreateAsync();
        var member = new Member
        {
            Id = Guid.NewGuid(), RunNo = "00001", FirstName = "มาลี", LastName = "ทดสอบ", District = "ร้องกวาง", Province = "แพร่",
            ApplicationDate = new DateOnly(2026, 1, 1), ApprovalDate = new DateOnly(2026, 1, 1), CoverageStartDate = new DateOnly(2026, 7, 1),
            AdvanceUnitsBalance = 7, Status = MemberStatus.Normal, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "test", UpdatedBy = "test"
        };
        db.Context.Members.Add(member);
        await db.Context.SaveChangesAsync();

        var service = new ResignationApplicationService(db.Context);
        var result = await service.ConfirmAsync(member.RunNo, new DateOnly(2026, 8, 25), DateTimeOffset.UtcNow, "tester");

        Assert.Equal(6_300, result.RefundSatang);
        Assert.Equal(MemberStatus.Resigned, member.Status);
        Assert.Equal(0, member.AdvanceUnitsBalance);
        Assert.Equal("resignation_refund", (await db.Context.AdvanceLedgerEntries.SingleAsync()).EntryType);
        Assert.Equal("resigned", (await db.Context.MemberStatusEvents.SingleAsync()).ToStatus);
        await Assert.ThrowsAsync<MemberValidationException>(() => service.ConfirmAsync(member.RunNo, new DateOnly(2026, 8, 25), DateTimeOffset.UtcNow, "tester"));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private TestDatabase(SqliteConnection connection, AppDbContext context) { this.connection = connection; Context = context; }
        public AppDbContext Context { get; }
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            context.SystemSettings.Single().WelfarePerMemberSatang = 900;
            await context.SaveChangesAsync();
            return new TestDatabase(connection, context);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
