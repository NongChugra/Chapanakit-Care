using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

internal sealed class FinanceTestStore(SqliteConnection connection, AppDbContext db) : IAsyncDisposable
{
    public AppDbContext Db { get; } = db;
    public static readonly DateOnly Date = new(2026, 9, 12);
    public static readonly DateTimeOffset Now = new(2026, 9, 12, 3, 0, 0, TimeSpan.Zero);

    public static async Task<FinanceTestStore> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return new(connection, db);
    }

    public async Task<Member> AddMemberAsync(string runNo, string group = "01")
    {
        var member = TestData.Member();
        member.Id = Guid.NewGuid();
        member.RunNo = runNo;
        member.GroupNo = group;
        member.ApplicationDate = Date.AddYears(-1);
        member.ApprovalDate = Date.AddYears(-1);
        member.CoverageStartDate = Date.AddMonths(-5);
        Db.Members.Add(member);
        for (var slot = 1; slot <= 2; slot++)
        {
            var beneficiary = TestData.Beneficiary(slot);
            beneficiary.Id = Guid.NewGuid();
            beneficiary.MemberId = member.Id;
            Db.MemberBeneficiaries.Add(beneficiary);
        }
        await Db.SaveChangesAsync();
        return member;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }
}
