using System.Net;
using System.Net.Http.Json;
using MySavings.ApiService.DTOs;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class MonthlyEntryEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public MonthlyEntryEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_NewMonth_ReturnsCreatedAndPersists()
    {
        var request = new CreateMonthlyEntryRequest
        {
            Year = 2024,
            Month = 1,
            Person1Salary = 3000m,
            Person2Salary = 2000m,
            EndMonthBeforeSalary = 500m
        };

        var response = await _client.PostAsJsonAsync("/api/monthly-entries", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        var entry = Assert.Single(verifyCtx.MonthlyEntries);
        Assert.Equal(new DateTime(2024, 1, 1), entry.Month);
        Assert.Equal(3000m, entry.Person1Salary);
        Assert.Equal(5000m, entry.TotalSalary);
    }

    [Fact]
    public async Task Create_UsesConfiguredSecurityBuffer_AffectsCalculatedExpenses()
    {
        await _client.PutAsJsonAsync("/api/app-settings", new { SecurityBuffer = 1000m });

        var request = new CreateMonthlyEntryRequest
        {
            Year = 2024,
            Month = 1,
            Person1Salary = 3000m,
            Person2Salary = 2000m,
            EndMonthBeforeSalary = 2000m
        };

        await _client.PostAsJsonAsync("/api/monthly-entries", request);

        // TotalExpenses = TotalSalary - EndMonthBeforeSalary + SecurityBuffer = 5000 - 2000 + 1000 = 4000
        // (would be 3500 with the default 500 buffer)
        await using var verifyCtx = _factory.CreateDbContext();
        var entry = Assert.Single(verifyCtx.MonthlyEntries);
        Assert.Equal(4000m, entry.TotalExpenses);
    }

    [Fact]
    public async Task Create_ExistingMonth_UpdatesInPlaceAndReturnsOk()
    {
        var request = new CreateMonthlyEntryRequest { Year = 2024, Month = 1, Person1Salary = 3000m, Person2Salary = 2000m };
        await _client.PostAsJsonAsync("/api/monthly-entries", request);

        var updateRequest = new CreateMonthlyEntryRequest { Year = 2024, Month = 1, Person1Salary = 3500m, Person2Salary = 2000m };
        var response = await _client.PostAsJsonAsync("/api/monthly-entries", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        var entry = Assert.Single(verifyCtx.MonthlyEntries);
        Assert.Equal(3500m, entry.Person1Salary);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/monthly-entries/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_CascadesToAllocations()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        int entryId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);

            var entry = new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 1000m, TotalSavings = 500m };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();
            entryId = entry.Id;

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entryId,
                SavingsAccountId = account.Id,
                Amount = 500m,
                Percentage = 1m,
                Weight = 1m
            });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/monthly-entries/{entryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.MonthlyEntries);
        Assert.Empty(verifyCtx.SavingsAllocations);
    }

    [Fact]
    public async Task Delete_LatestMonthWithPendingAccumulatorContribution_RestoresAccountAccumulator()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        int entryId;
        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount
            {
                Name = "Assu Vie Fortuneo Louis",
                OwnerId = 7,
                TransferThreshold = 100m,
                TransferAccumulator = 50m // this month's allocation carried it from 0 to 50
            };
            seedCtx.SavingsAccounts.Add(account);

            var entry = new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 1000m, TotalSavings = 500m };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();
            entryId = entry.Id;
            accountId = account.Id;

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entryId,
                SavingsAccountId = accountId,
                Amount = 50m,
                Percentage = 0.1m,
                Weight = 0.05m,
                AccumulatorBefore = 0m,
                TransferableAmount = 0m
            });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/monthly-entries/{entryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        var updatedAccount = await verifyCtx.SavingsAccounts.FindAsync(accountId);
        Assert.Equal(0m, updatedAccount!.TransferAccumulator);
    }
}
