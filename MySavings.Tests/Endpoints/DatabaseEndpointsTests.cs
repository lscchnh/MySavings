using System.Net.Http.Json;
using System.Text.Json;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class DatabaseEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public DatabaseEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Diagnostic_ReturnsEntriesForRequestedMonthOnly()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            seedCtx.MonthlyEntries.AddRange(
                new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 1000m },
                new MonthlyEntry { Month = new DateTime(2024, 2, 1), TotalSalary = 2000m });
            await seedCtx.SaveChangesAsync();
        }

        var body = await _client.GetFromJsonAsync<JsonElement>("/api/database/diagnostic/2024/1");

        Assert.Equal("2024-01", body.GetProperty("month").GetString());
        var entries = body.GetProperty("entries").EnumerateArray().ToList();
        Assert.Single(entries);
        Assert.Equal(1000, entries[0].GetProperty("totalSalary").GetDecimal());

        var allAccounts = body.GetProperty("allAccounts").EnumerateArray().ToList();
        Assert.Single(allAccounts);
        Assert.Equal("PEA Louis", allAccounts[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Clear_RemovesAllocationsEntriesRulesAndTransferGroups_ButKeepsAccountsAndOwners()
    {
        await _factory.ClearOwnersAsync();
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var owner = new Owner { Id = 7, Name = "Louis" };
            seedCtx.Owners.Add(owner);

            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();

            seedCtx.AllocationRules.Add(new AllocationRule { SavingsAccountId = account.Id, Weight = 0.5m });
            seedCtx.TransferGroups.Add(new TransferGroup { Name = "Virement mensuel", SavingsAccounts = { account } });

            var entry = new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 1000m, TotalSavings = 500m };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entry.Id,
                SavingsAccountId = account.Id,
                Amount = 250m,
                Percentage = 0.5m,
                Weight = 0.5m
            });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync("/api/database/clear");
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.SavingsAllocations);
        Assert.Empty(verifyCtx.MonthlyEntries);
        Assert.Empty(verifyCtx.AllocationRules);
        Assert.Empty(verifyCtx.TransferGroups);

        // Accounts and Owners are not wiped by "clear"
        Assert.Single(verifyCtx.SavingsAccounts);
        Assert.Single(verifyCtx.Owners);
    }
}
