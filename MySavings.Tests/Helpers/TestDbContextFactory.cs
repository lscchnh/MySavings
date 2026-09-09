using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;

namespace MySavings.Tests.Helpers;

/// <summary>
/// Creates an in-memory SQLite database for testing.
/// The shared connection must stay open for the lifetime of the test.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        ctx.Owners.AddRange(
            new Owner { Id = 1, Name = "Person1" },
            new Owner { Id = 2, Name = "Person2" }
        );
        ctx.SaveChanges();
    }

    public MySavingsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MySavingsDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new MySavingsDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
