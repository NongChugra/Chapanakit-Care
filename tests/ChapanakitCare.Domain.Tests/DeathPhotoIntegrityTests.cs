using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChapanakitCare.Domain.Tests;

public sealed class DeathPhotoIntegrityTests
{
    private static readonly DateOnly Today = new(2026, 9, 8);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0ioAAAAASUVORK5CYII=");
    private static ConfirmDeathCommand Command => new("00001", "DC-1", Today.AddDays(-1), "เหตุ", false, null,
        new("certificate.pdf", "application/pdf", "%PDF-1.4\n%%EOF"u8.ToArray()),
        [new(1, "../../recipient.png", "text/html", Png)], Today);

    [Fact]
    public async Task Failure_writing_audit_rolls_back_photo_snapshots_status_and_counter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection.ConnectionString);
        db.Database.SetDbConnection(connection);
        await db.Database.MigrateAsync();
        await Seed(db);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_death_audit BEFORE INSERT ON audit_events BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => new DeathApplicationService(db).ConfirmAsync(Command, Today, DateTimeOffset.UtcNow, "test"));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.DeathCases.ToListAsync());
        Assert.Empty(await db.DeathRecipientPhotos.ToListAsync());
        Assert.Empty(await db.DeathMemberSnapshots.ToListAsync());
        Assert.Empty(await db.AdvanceLedgerEntries.ToListAsync());
        var member = await db.Members.SingleAsync();
        Assert.Equal(MemberStatus.Normal, member.Status);
        Assert.Equal(30, member.AdvanceUnitsBalance);
    }

    [Theory]
    [InlineData(2569, 9, 7, 2026, 9, 8)]
    [InlineData(2026, 9, 7, 2026, 9, 6)]
    [InlineData(1461, 9, 7, 2026, 9, 8)]
    public async Task Death_rejects_future_or_pre_membership_death_and_reporting_before_death(int y, int m, int d, int ry, int rm, int rd)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection.ConnectionString);
        db.Database.SetDbConnection(connection);
        await db.Database.EnsureCreatedAsync();
        await Seed(db);
        await Assert.ThrowsAsync<MemberValidationException>(() => new DeathApplicationService(db).ConfirmAsync(
            Command with { DeathCertificateDate = new(y, m, d), ReportedCertificateDate = new(ry, rm, rd) },
            Today, DateTimeOffset.UtcNow, "test"));
        Assert.Empty(await db.DeathCases.ToListAsync());
    }

    [Fact]
    public async Task Upgrade_and_backup_preserve_audit_identity_and_recipient_image_bytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "recipient-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var db = Context($"Data Source={Path.Combine(directory, "source.db")};Pooling=False");
            await db.GetService<IMigrator>().MigrateAsync("20260904022726_CoordinatorSelection");
            await Seed(db);
            var member = await db.Members.SingleAsync();
            db.AuditEvents.Add(new() { Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), Action = "member.created",
                MemberId = member.Id, EntityId = member.Id.ToString(), OccurredAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(member.Id, (await db.AuditEvents.SingleAsync()).MemberId);
            await new DeathApplicationService(db).ConfirmAsync(Command, Today, DateTimeOffset.UtcNow, "test");
            var backup = await new BackupApplicationService(db).CreateAsync(directory, DateTimeOffset.UtcNow, "test");
            await using var restored = Context($"Data Source={backup.FullPath};Pooling=False");
            var image = await restored.DeathRecipientPhotos.SingleAsync();
            Assert.Equal(Png, image.Bytes);
            Assert.Equal("recipient-1.png", image.FileName);
            Assert.Equal("image/png", image.ContentType);
            Assert.Equal(64, image.Sha256.Length);
            Assert.Equal(Today, (await restored.DeathCases.SingleAsync()).ReportedCertificateDate);
            await new DemoDataMaintenanceService(db).ClearMembersAsync(DateTimeOffset.UtcNow, "test");
            Assert.Empty(await db.DeathRecipientPhotos.ToListAsync());
            Assert.Equal(3, await db.AuditEvents.CountAsync());
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task Seed(AppDbContext db)
    {
        db.Members.Add(TestData.Member());
        db.MemberBeneficiaries.Add(TestData.Beneficiary(1));
        await db.SaveChangesAsync();
    }
    private static AppDbContext Context(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
}
