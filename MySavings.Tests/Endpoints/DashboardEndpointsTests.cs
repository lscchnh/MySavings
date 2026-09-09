using System.Net.Http.Json;
using MySavings.ApiService.DTOs;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class DashboardEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public DashboardEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Summary_EmptyDb_ReturnsZeroValuesAndNoMonths()
    {
        var summary = await _client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");

        Assert.NotNull(summary);
        Assert.Equal(0, summary!.TotalSalary);
        Assert.Equal(0, summary.TotalSavings);
        Assert.Empty(summary.Months);
    }

    [Fact]
    public async Task Summary_WithEntries_ReturnsLatestMonthAndTrends()
    {
        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.MonthlyEntries.AddRange(
                new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 4000m, TotalExpenses = 3000m, TotalSavings = 1000m, TotalSavingsRatio = 0.25m },
                new MonthlyEntry { Month = new DateTime(2024, 2, 1), TotalSalary = 5000m, TotalExpenses = 3000m, TotalSavings = 2000m, TotalSavingsRatio = 0.4m });
            await seedCtx.SaveChangesAsync();
        }

        var summary = await _client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");

        Assert.NotNull(summary);
        Assert.Equal(new DateTime(2024, 2, 1), summary!.CurrentMonth);
        Assert.Equal(5000m, summary.TotalSalary);
        Assert.Equal(2000m, summary.TotalSavings);
        Assert.Equal(0.4m, summary.SavingsRatio);
        Assert.Equal(2, summary.Months.Count);
        Assert.Equal(new DateTime(2024, 1, 1), summary.Months[0].Month);
    }

    [Fact]
    public async Task History_EmptyDb_ReturnsEmptyList()
    {
        var history = await _client.GetFromJsonAsync<List<ConsolidatedViewDto>>("/api/dashboard/history");

        Assert.NotNull(history);
        Assert.Empty(history!);
    }

    [Fact]
    public async Task History_WithEntryAndAllocations_ReturnsOrderedConsolidatedView()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();

            var entry = new MonthlyEntry
            {
                Month = new DateTime(2024, 1, 1),
                Person1Salary = 3000m,
                Person2Salary = 2000m,
                TotalSalary = 5000m,
                TotalSavings = 1000m
            };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entry.Id,
                SavingsAccountId = account.Id,
                Amount = 400m,
                Percentage = 0.4m,
                Weight = 0.4m,
                IsTransferred = true
            });
            await seedCtx.SaveChangesAsync();
        }

        var history = await _client.GetFromJsonAsync<List<ConsolidatedViewDto>>("/api/dashboard/history");

        Assert.NotNull(history);
        var view = Assert.Single(history!);
        Assert.Equal(new DateTime(2024, 1, 1), view.Month);
        var allocation = Assert.Single(view.Allocations);
        Assert.Equal("PEA Louis", allocation.AccountName);
        Assert.Equal(7, allocation.OwnerId);
        Assert.Equal(400m, allocation.Amount);
        Assert.True(allocation.IsTransferred);
    }

    [Fact]
    public async Task History_AllocationBelowThreshold_ReturnsZeroTransferableAmount()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "Assu Vie Fortuneo Louis", OwnerId = 7, TransferThreshold = 100m, TransferAccumulator = 60m };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();

            var entry = new MonthlyEntry
            {
                Month = new DateTime(2024, 1, 1),
                Person1Salary = 3000m,
                Person2Salary = 2000m,
                TotalSalary = 5000m,
                TotalSavings = 1000m
            };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entry.Id,
                SavingsAccountId = account.Id,
                Amount = 60m,
                Percentage = 0.06m,
                Weight = 0.06m,
                AccumulatorBefore = 0m,
                TransferableAmount = 0m
            });
            await seedCtx.SaveChangesAsync();
        }

        var history = await _client.GetFromJsonAsync<List<ConsolidatedViewDto>>("/api/dashboard/history");

        var view = Assert.Single(history!);
        var allocation = Assert.Single(view.Allocations);
        Assert.Equal(60m, allocation.Amount);
        Assert.Equal(0m, allocation.TransferableAmount);
    }
}
