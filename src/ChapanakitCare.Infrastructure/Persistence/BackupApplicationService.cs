using System.Data;
using System.Security.Cryptography;
using ChapanakitCare.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Persistence;

public sealed record BackupResult(string FileName, string FullPath, long FileSize, string Sha256);

public sealed class BackupApplicationService(AppDbContext database)
{
    public async Task<BackupResult> CreateAsync(
        string outputDirectory,
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var fileName = $"chapanakit-care-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db";
        var fullPath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
        var resolvedDirectory = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(resolvedDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ตำแหน่งไฟล์สำรองไม่ถูกต้อง");

        try
        {
            var source = (SqliteConnection)database.Database.GetDbConnection();
            if (source.State != ConnectionState.Open)
                await source.OpenAsync(cancellationToken);
            await using (var destination = new SqliteConnection($"Data Source={fullPath};Pooling=False"))
            {
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
                await using var integrity = destination.CreateCommand();
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = await integrity.ExecuteScalarAsync(cancellationToken);
                if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("ตรวจสอบความสมบูรณ์ของไฟล์สำรองไม่ผ่าน");
            }

            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            database.BackupRuns.Add(new BackupRun
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now,
                CreatedBy = actor,
                FileName = fileName,
                FileSize = bytes.LongLength,
                Sha256 = sha256,
                SchemaVersion = "checkpoint-5",
                IsAutomatic = false
            });
            await database.SaveChangesAsync(cancellationToken);
            return new BackupResult(fileName, fullPath, bytes.LongLength, sha256);
        }
        catch
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
            throw;
        }
    }
}
