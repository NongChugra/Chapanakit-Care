using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class RegistrationAgeTests
{
    [Theory]
    [InlineData(2006, 7, 20, false)]
    [InlineData(2006, 7, 19, true)]
    [InlineData(1966, 7, 19, true)]
    [InlineData(1965, 7, 20, true)]
    [InlineData(1965, 7, 19, false)]
    [InlineData(1461, 7, 19, false)]
    public void Eligibility_uses_completed_age_on_registration_date(int year, int month, int day, bool eligible)
    {
        var issues = InteractiveMemberRegistrationPolicy.Validate(Command(new DateOnly(year, month, day)));
        Assert.Equal(!eligible, issues.Any(x => x.Field == "BirthDate"));
    }

    [Fact]
    public async Task Service_rejects_underage_registration_before_allocating_number_or_writing_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.NumberSequences.Add(new() { SequenceKey = "member_run_no", NextValue = 1, Width = 5 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<MemberValidationException>(() => new MemberApplicationService(db).RegisterAsync(
            Command(new DateOnly(2006, 7, 20)), new DateOnly(2026, 7, 19), DateTimeOffset.UtcNow, "test"));
        Assert.Empty(await db.Members.ToListAsync());
        Assert.Empty(await db.AuditEvents.ToListAsync());
        Assert.Equal(1, (await db.NumberSequences.SingleAsync()).NextValue);
    }

    [Theory]
    [InlineData(2026, 7, 18)]
    [InlineData(2569, 7, 19)]
    public async Task Registration_rejects_approval_before_application_or_a_buddhist_year_in_storage(int year, int month, int day)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.NumberSequences.Add(new() { SequenceKey = "member_run_no", NextValue = 1, Width = 5 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<MemberValidationException>(() => new MemberApplicationService(db).RegisterAsync(
            Command(new(1980, 1, 1)) with { ApprovalDate = new(year, month, day) },
            new(2026, 7, 19), DateTimeOffset.UtcNow, "test"));
    }

    internal static RegisterMemberCommand Command(DateOnly birth) => new(
        "นาย", "สมาชิก", "ทดสอบ", "ชาย", "1234567890123", birth, "11", "หมู่บ้าน", "1", "ร้องกวาง",
        "54140", null, "0101", new DateOnly(2026, 7, 19), new DateOnly(2026, 7, 19), [], "ร้องกวาง", "แพร่");
}
