using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;

namespace MySavings.Tests.Helpers;

/// <summary>
/// Spins up the ApiService pipeline (Program.cs) end-to-end against an isolated
/// in-memory SQLite database, so endpoint tests exercise real routing/binding/serialization.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MySavingsDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<MySavingsDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Opens a fresh context bound to the same in-memory connection, for seeding/verifying
    /// data outside of the HTTP pipeline (mirrors TestDbContextFactory's pattern).
    /// </summary>
    public MySavingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MySavingsDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new MySavingsDbContext(options);
    }

    /// <summary>
    /// Seeds Owner rows (idempotently) so SavingsAccount.OwnerId FK inserts don't fail.
    /// Most endpoint tests don't care about Owner semantics directly and just need
    /// some valid owner ids to reference, e.g. await SeedOwnersAsync((7, "Louis"), (8, "Alice")).
    /// </summary>
    public async Task SeedOwnersAsync(params (int Id, string Name)[] owners)
    {
        await using var ctx = CreateDbContext();
        foreach (var (id, name) in owners)
        {
            if (!await ctx.Owners.AnyAsync(o => o.Id == id))
                ctx.Owners.Add(new Owner { Id = id, Name = name });
        }
        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Sets an owner's target allocation share (0-1), used by tests that exercise the
    /// per-owner weight-sum validation in the allocation-rules bulk endpoint.
    /// </summary>
    public async Task SetOwnerShareAsync(int ownerId, decimal sharePercent)
    {
        await using var ctx = CreateDbContext();
        var owner = await ctx.Owners.FindAsync(ownerId);
        if (owner != null)
        {
            owner.AllocationSharePercent = sharePercent;
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Removes owners seeded by the SeedDefaultPersonSettings migration, for tests that need
    /// full control over the Owner table's contents.
    /// </summary>
    public async Task ClearOwnersAsync()
    {
        await using var ctx = CreateDbContext();
        ctx.Owners.RemoveRange(ctx.Owners);
        await ctx.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
