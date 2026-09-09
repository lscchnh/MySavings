using System.Net;
using System.Net.Http.Json;
using MySavings.ApiService.Endpoints;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class SavingsAllocationEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public SavingsAllocationEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SetTransferred_NotFound_ReturnsNotFound()
    {
        var response = await _client.PatchAsJsonAsync("/api/savings-allocations/999/transferred", new SetTransferredRequest(true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetTransferred_Existing_UpdatesFlag()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        int allocationId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            var entry = new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 1000m, TotalSavings = 500m };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();

            var allocation = new SavingsAllocation
            {
                MonthlyEntryId = entry.Id,
                SavingsAccountId = account.Id,
                Amount = 500m,
                Percentage = 1m,
                Weight = 1m,
                IsTransferred = false
            };
            seedCtx.SavingsAllocations.Add(allocation);
            await seedCtx.SaveChangesAsync();
            allocationId = allocation.Id;
        }

        var response = await _client.PatchAsJsonAsync($"/api/savings-allocations/{allocationId}/transferred", new SetTransferredRequest(true));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        var updatedAllocation = await verifyCtx.SavingsAllocations.FindAsync(allocationId);
        Assert.True(updatedAllocation!.IsTransferred);
    }
}
