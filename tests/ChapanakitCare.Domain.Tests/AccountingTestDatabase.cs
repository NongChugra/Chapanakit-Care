using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

internal sealed class AccountingTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private AccountingTestDatabase(SqliteConnection connection, AppDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public AppDbContext Context { get; }

    public static async Task<AccountingTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging(false)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new AccountingTestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
