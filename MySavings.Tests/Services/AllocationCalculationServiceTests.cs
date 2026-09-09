using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySavings.ApiService.Models;
using MySavings.ApiService.Services;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Services;

public class AllocationCalculationServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    private static SavingsAccount MakeAccount(string name, int? ownerId, decimal weight, int id = 0, decimal transferThreshold = 0m)
    {
        var account = new SavingsAccount { Name = name, OwnerId = ownerId, TransferThreshold = transferThreshold };
        if (id > 0)
            account.Id = id;
        account.AllocationRule = new AllocationRule { Weight = weight };
        return account;
    }

    private static MonthlyEntry MakeEntry(
        decimal totalSavings,
        decimal prevP1Pct = 0.6m,
        decimal prevP2Pct = 0.4m,
        DateTime? month = null)
    {
        return new MonthlyEntry
        {
            Month = month ?? new DateTime(2024, 1, 1),
            TotalSavings = totalSavings,
            Person1Salary = 3000m,
            Person2Salary = 2000m,
            TotalSalary = 5000m,
            PreviousMonthPerson1Percent = prevP1Pct,
            PreviousMonthPerson2Percent = prevP2Pct
        };
    }

    private AllocationCalculationService BuildService()
    {
        var ctx = _factory.CreateContext();
        return new AllocationCalculationService(ctx, NullLogger<AllocationCalculationService>.Instance);
    }

    // ─────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateAllocationsAsync_NonExistentEntry_ReturnsZero()
    {
        var svc = BuildService();
        var result = await svc.CalculateAllocationsAsync(999);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_ZeroTotalSavings_ClearsExistingAndReturnsZero()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("PEA", ownerId: 1, weight: 0.5m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(0m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        ctx.SavingsAllocations.Add(new SavingsAllocation
        {
            MonthlyEntryId = entry.Id,
            SavingsAccountId = account.Id,
            Amount = 100m,
            Percentage = 0.1m,
            Weight = 0.5m
        });
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var result = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(0, result);

        await using var verifyCtx = _factory.CreateContext();
        Assert.Equal(0, verifyCtx.SavingsAllocations.Count());
    }

    [Fact]
    public async Task CalculateAllocationsAsync_Person1Account_UsesFormula()
    {
        // Formula: Amount = Weight × (OwnerPercent_M-1 / 0.5) × TotalSavings
        // OwnerId=1 (Person1): prevPercent=0.6, Weight=0.25, TotalSavings=2000
        // Expected = 0.25 × (0.6 / 0.5) × 2000 = 0.25 × 1.2 × 2000 = 600
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("PEA", ownerId: 1, weight: 0.25m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 2000m, prevP1Pct: 0.6m, prevP2Pct: 0.4m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var count = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(1, count);

        await using var verifyCtx = _factory.CreateContext();
        var allocation = verifyCtx.SavingsAllocations.Single();
        Assert.Equal(600m, allocation.Amount);
        Assert.Equal(600m / 2000m, allocation.Percentage);
        Assert.Equal(0.25m, allocation.Weight);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_Person2Account_UsesFormula()
    {
        // OwnerId=2 (Person2): prevPercent=0.4, Weight=0.3, TotalSavings=1000
        // Expected = 0.3 × (0.4 / 0.5) × 1000 = 0.3 × 0.8 × 1000 = 240
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("LDD", ownerId: 2, weight: 0.3m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.6m, prevP2Pct: 0.4m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        var allocation = verifyCtx.SavingsAllocations.Single();
        Assert.Equal(240m, allocation.Amount);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_EqualPrevPercents_AmountEqualsWeightTimesTotalSavings()
    {
        // When Person1Percent = Person2Percent = 0.5, the factor (Pct/0.5) = 1
        // Amount = Weight × 1 × TotalSavings = Weight × TotalSavings
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("AV", ownerId: 1, weight: 0.4m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        var allocation = verifyCtx.SavingsAllocations.Single();
        Assert.Equal(400m, allocation.Amount);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_NullOwner_SkipsAccount()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("SHARED", ownerId: null, weight: 0.5m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var count = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(0, count);

        await using var verifyCtx = _factory.CreateContext();
        Assert.Equal(0, verifyCtx.SavingsAllocations.Count());
    }

    [Fact]
    public async Task CalculateAllocationsAsync_NullOwnerAccount_WithOnlyOneOwnerInDb_SkipsAccount()
    {
        // Reproduces a bug where comparing two nullable ints with `==` let a null-owner account
        // slip through as if it belonged to Person1/Person2 whenever person2OwnerId (or even
        // person1OwnerId) was itself null, i.e. fewer than 2 owners exist in the database.
        await using (var setupCtx = _factory.CreateContext())
        {
            var secondOwner = await setupCtx.Owners.FindAsync(2);
            setupCtx.Owners.Remove(secondOwner!);
            await setupCtx.SaveChangesAsync();
        }

        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("SHARED", ownerId: null, weight: 0.5m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var count = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(0, count);

        await using var verifyCtx = _factory.CreateContext();
        Assert.Equal(0, verifyCtx.SavingsAllocations.Count());
    }

    [Fact]
    public async Task CalculateAllocationsAsync_AccountWithNoRule_IsSkipped()
    {
        await using var ctx = _factory.CreateContext();
        var account = new SavingsAccount { Name = "No Rule", OwnerId = 1 };
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var count = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_ExistingAllocation_IsUpdated()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("PEA", ownerId: 1, weight: 0.5m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id);

        await using var updateCtx = _factory.CreateContext();
        var rule = updateCtx.AllocationRules.Single();
        rule.Weight = 0.3m;
        await updateCtx.SaveChangesAsync();

        var svc2 = BuildService();
        await svc2.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        var allocations = verifyCtx.SavingsAllocations.ToList();
        Assert.Single(allocations);
        Assert.Equal(300m, allocations[0].Amount); // 0.3 × 1.0 × 1000
        Assert.Equal(0.3m, allocations[0].Weight);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_ZeroWeightRule_AccountIsSkipped()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("ZERO", ownerId: 1, weight: 0m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var count = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CalculateAllocationsForMonthAsync_UnknownMonth_ReturnsZero()
    {
        var svc = BuildService();
        var result = await svc.CalculateAllocationsForMonthAsync(new DateTime(2099, 1, 1));
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_MultipleAccounts_AllAllocationsCreated()
    {
        await using var ctx = _factory.CreateContext();
        ctx.SavingsAccounts.AddRange(
            MakeAccount("PEA", ownerId: 1, weight: 0.3m),
            MakeAccount("AV", ownerId: 2, weight: 0.2m),
            MakeAccount("CRYPTO", ownerId: 1, weight: 0.1m)
        );
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 2000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        var count = await svc.CalculateAllocationsAsync(entry.Id);

        Assert.Equal(3, count);

        await using var verifyCtx = _factory.CreateContext();
        var allocs = verifyCtx.SavingsAllocations.ToList().OrderBy(a => a.Amount).ToList();
        Assert.Equal(200m, allocs[0].Amount);  // CRYPTO: 0.1 × 1 × 2000
        Assert.Equal(400m, allocs[1].Amount);  // AV:     0.2 × 1 × 2000
        Assert.Equal(600m, allocs[2].Amount);  // PEA:    0.3 × 1 × 2000
    }

    [Fact]
    public async Task DeleteMonthlyEntry_CascadeDeletesSavingsAllocations()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("PEA", ownerId: 1, weight: 0.5m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        Assert.Equal(1, verifyCtx.SavingsAllocations.Count());

        await using var deleteCtx = _factory.CreateContext();
        var entryToDelete = await deleteCtx.MonthlyEntries.FindAsync(entry.Id);
        deleteCtx.MonthlyEntries.Remove(entryToDelete!);
        await deleteCtx.SaveChangesAsync();

        await using var finalCtx = _factory.CreateContext();
        Assert.Equal(0, finalCtx.SavingsAllocations.Count());
    }

    // ─────────────────────────────────────────────────────────
    // Transfer threshold / accumulator
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateAllocationsAsync_ZeroThreshold_TransferableAmountEqualsAmount()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("PEA", ownerId: 1, weight: 0.5m, transferThreshold: 0m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        var allocation = verifyCtx.SavingsAllocations.Single();
        Assert.Equal(500m, allocation.Amount);
        Assert.Equal(500m, allocation.TransferableAmount);
        Assert.Equal(0m, verifyCtx.SavingsAccounts.Single(a => a.Id == account.Id).TransferAccumulator);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_BelowThreshold_TransferableAmountIsZeroAndAccumulates()
    {
        await using var ctx = _factory.CreateContext();
        // weight 0.05 × totalSavings 1000 = 50, below the 100 threshold.
        var account = MakeAccount("Assu Vie Fortuneo", ownerId: 1, weight: 0.05m, transferThreshold: 100m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        var allocation = verifyCtx.SavingsAllocations.Single();
        Assert.Equal(50m, allocation.Amount);
        Assert.Equal(0m, allocation.TransferableAmount);
        Assert.Equal(50m, verifyCtx.SavingsAccounts.Single(a => a.Id == account.Id).TransferAccumulator);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_CrossingThresholdAcrossTwoMonths_TransfersAccumulatedTotalAndResets()
    {
        // Matches the user's example: month X = 50€ (held back), month X+1 = 60€,
        // so month X+1's transfer becomes 50 + 60 = 110€ and the accumulator resets.
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("Assu Vie Fortuneo", ownerId: 1, weight: 0.05m, transferThreshold: 100m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var monthX = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m, month: new DateTime(2024, 1, 1));
        ctx.MonthlyEntries.Add(monthX);
        await ctx.SaveChangesAsync();

        var svc1 = BuildService();
        await svc1.CalculateAllocationsAsync(monthX.Id);

        await using (var midCtx = _factory.CreateContext())
        {
            var afterMonthX = midCtx.SavingsAllocations.Single();
            Assert.Equal(0m, afterMonthX.TransferableAmount);
            Assert.Equal(50m, midCtx.SavingsAccounts.Single(a => a.Id == account.Id).TransferAccumulator);
        }

        await using (var ctx2 = _factory.CreateContext())
        {
            var monthX1 = MakeEntry(totalSavings: 1200m, prevP1Pct: 0.5m, prevP2Pct: 0.5m, month: new DateTime(2024, 2, 1));
            ctx2.MonthlyEntries.Add(monthX1);
            await ctx2.SaveChangesAsync();

            var svc2 = new AllocationCalculationService(ctx2, NullLogger<AllocationCalculationService>.Instance);
            await svc2.CalculateAllocationsAsync(monthX1.Id);
        }

        await using var verifyCtx = _factory.CreateContext();
        var monthX1Allocation = verifyCtx.SavingsAllocations
            .Include(a => a.MonthlyEntry)
            .Single(a => a.MonthlyEntry.Month == new DateTime(2024, 2, 1));

        Assert.Equal(60m, monthX1Allocation.Amount);
        Assert.Equal(110m, monthX1Allocation.TransferableAmount);
        Assert.Equal(0m, verifyCtx.SavingsAccounts.Single(a => a.Id == account.Id).TransferAccumulator);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_RecalculatingBelowThresholdMonth_ReAdjustsAccumulatorInsteadOfDoubleCounting()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("Assu Vie Fortuneo", ownerId: 1, weight: 0.05m, transferThreshold: 100m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id); // Amount 50, held back, accumulator -> 50

        // Correct the rule's weight so the recalculated amount alone crosses the threshold.
        await using (var editCtx = _factory.CreateContext())
        {
            var rule = editCtx.AllocationRules.Single();
            rule.Weight = 0.2m; // 0.2 × 1000 = 200
            await editCtx.SaveChangesAsync();
        }

        var svc2 = BuildService();
        await svc2.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        var allocation = verifyCtx.SavingsAllocations.Single();
        Assert.Equal(200m, allocation.Amount);
        // If the accumulator had not been re-adjusted, this would incorrectly be 50 + 200 = 250.
        Assert.Equal(200m, allocation.TransferableAmount);
        Assert.Equal(0m, verifyCtx.SavingsAccounts.Single(a => a.Id == account.Id).TransferAccumulator);
    }

    [Fact]
    public async Task CalculateAllocationsAsync_TotalSavingsClearsAllocations_UndoesAccumulatorContribution()
    {
        await using var ctx = _factory.CreateContext();
        var account = MakeAccount("Assu Vie Fortuneo", ownerId: 1, weight: 0.05m, transferThreshold: 100m);
        ctx.SavingsAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var entry = MakeEntry(totalSavings: 1000m, prevP1Pct: 0.5m, prevP2Pct: 0.5m);
        ctx.MonthlyEntries.Add(entry);
        await ctx.SaveChangesAsync();

        var svc = BuildService();
        await svc.CalculateAllocationsAsync(entry.Id); // accumulator -> 50

        await using (var editCtx = _factory.CreateContext())
        {
            var entryToClear = await editCtx.MonthlyEntries.FindAsync(entry.Id);
            entryToClear!.TotalSavings = -100m;
            await editCtx.SaveChangesAsync();
        }

        var svc2 = BuildService();
        await svc2.CalculateAllocationsAsync(entry.Id);

        await using var verifyCtx = _factory.CreateContext();
        Assert.Empty(verifyCtx.SavingsAllocations);
        Assert.Equal(0m, verifyCtx.SavingsAccounts.Single(a => a.Id == account.Id).TransferAccumulator);
    }
}
