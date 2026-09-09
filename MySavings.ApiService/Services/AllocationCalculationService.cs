using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;

namespace MySavings.ApiService.Services;

/// <summary>
/// Service to calculate savings allocations for monthly entries
/// Formula: Amount = Weight × (OwnerPercent_M-1 / 0.5) × TotalSavings
/// </summary>
public class AllocationCalculationService(MySavingsDbContext db, ILogger<AllocationCalculationService> logger)
{
    private readonly MySavingsDbContext _db = db;
    private readonly ILogger<AllocationCalculationService> _logger = logger;

    /// <summary>
    /// Calculates and creates/updates allocations for a monthly entry.
    /// The two owners with the lowest Id are treated as Person1/Person2, matching
    /// MonthlyEntry.PreviousMonthPerson1Percent/PreviousMonthPerson2Percent ordinal positions
    /// rather than fixed literal owner ids (which don't necessarily start at 1/2 in practice).
    /// </summary>
    public async Task<int> CalculateAllocationsAsync(int monthlyEntryId)
    {
        var monthlyEntry = await _db.MonthlyEntries
            .Include(e => e.SavingsAllocations)
            .FirstOrDefaultAsync(e => e.Id == monthlyEntryId);

        if (monthlyEntry == null)
        {
            _logger.LogWarning("Monthly entry {Id} not found", monthlyEntryId);
            return 0;
        }

        decimal totalSavings = monthlyEntry.TotalSavings;

        if (totalSavings <= 0)
        {
            if (monthlyEntry.SavingsAllocations.Count > 0)
            {
                // Undo each allocation's contribution to its account's transfer accumulator
                // before removing it, so the accumulator doesn't retain a phantom contribution.
                foreach (var removedAllocation in monthlyEntry.SavingsAllocations)
                {
                    var ownerAccount = await _db.SavingsAccounts.FindAsync(removedAllocation.SavingsAccountId);
                    if (ownerAccount != null)
                        ownerAccount.TransferAccumulator = removedAllocation.AccumulatorBefore;
                }

                _db.SavingsAllocations.RemoveRange(monthlyEntry.SavingsAllocations);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Total savings is zero or negative for {Month:yyyy-MM}, allocations cleared", monthlyEntry.Month);
            return 0;
        }

        var orderedOwnerIds = await _db.Owners.OrderBy(o => o.Id).Select(o => o.Id).ToListAsync();
        int? person1OwnerId = orderedOwnerIds.Count > 0 ? orderedOwnerIds[0] : null;
        int? person2OwnerId = orderedOwnerIds.Count > 1 ? orderedOwnerIds[1] : null;

        var accountsWithRules = await _db.SavingsAccounts
            .Include(a => a.AllocationRule)
            .Where(a => a.AllocationRule != null && a.AllocationRule.Weight > 0)
            .ToListAsync();

        int allocationsProcessed = 0;

        foreach (var account in accountsWithRules)
        {
            var rule = account.AllocationRule!;

            decimal ownerPercentM1;
            if (account.OwnerId.HasValue && account.OwnerId == person1OwnerId)
            {
                ownerPercentM1 = monthlyEntry.PreviousMonthPerson1Percent;
            }
            else if (account.OwnerId.HasValue && account.OwnerId == person2OwnerId)
            {
                ownerPercentM1 = monthlyEntry.PreviousMonthPerson2Percent;
            }
            else
            {
                _logger.LogWarning("Account {Name} has no valid owner (OwnerId={OwnerId}), skipping", account.Name, account.OwnerId);
                continue;
            }

            decimal amount = rule.Weight * (ownerPercentM1 / 0.5m) * totalSavings;

            if (amount <= 0)
                continue;

            decimal percentage = amount / totalSavings;

            var existingAllocation = monthlyEntry.SavingsAllocations
                .FirstOrDefault(a => a.SavingsAccountId == account.Id);

            // If this month's allocation for this account already existed, undo its previous
            // contribution to the account's transfer accumulator before reapplying with the
            // freshly recalculated amount, so the transferable amount always reflects the
            // latest recalculation rather than double-counting or drifting.
            if (existingAllocation != null)
                account.TransferAccumulator = existingAllocation.AccumulatorBefore;

            decimal accumulatorBefore = account.TransferAccumulator;
            decimal transferableAmount;

            if (account.TransferThreshold <= 0)
            {
                transferableAmount = amount;
                account.TransferAccumulator = 0;
            }
            else
            {
                decimal accumulated = accumulatorBefore + amount;
                if (accumulated >= account.TransferThreshold)
                {
                    transferableAmount = accumulated;
                    account.TransferAccumulator = 0;
                }
                else
                {
                    transferableAmount = 0;
                    account.TransferAccumulator = accumulated;
                }
            }

            if (existingAllocation == null)
            {
                var allocation = new SavingsAllocation
                {
                    MonthlyEntryId = monthlyEntry.Id,
                    SavingsAccountId = account.Id,
                    Amount = amount,
                    Percentage = percentage,
                    Weight = rule.Weight,
                    AccumulatorBefore = accumulatorBefore,
                    TransferableAmount = transferableAmount
                };
                _db.SavingsAllocations.Add(allocation);
                _logger.LogInformation("Created allocation: {Name} = {Amount:N2}€ (weight: {Weight:N4}, ownerPercent M-1: {Pct:N4}, transferable: {Transferable:N2}€)", account.Name, amount, rule.Weight, ownerPercentM1, transferableAmount);
            }
            else
            {
                existingAllocation.Amount = amount;
                existingAllocation.Percentage = percentage;
                existingAllocation.Weight = rule.Weight;
                existingAllocation.AccumulatorBefore = accumulatorBefore;
                existingAllocation.TransferableAmount = transferableAmount;
                _logger.LogInformation("Updated allocation: {Name} = {Amount:N2}€ (weight: {Weight:N4}, ownerPercent M-1: {Pct:N4}, transferable: {Transferable:N2}€)", account.Name, amount, rule.Weight, ownerPercentM1, transferableAmount);
            }

            allocationsProcessed++;
        }

        await _db.SaveChangesAsync();
        return allocationsProcessed;
    }

    /// <summary>
    /// Calculates allocations for the entry of a specific month.
    /// </summary>
    public async Task<int> CalculateAllocationsForMonthAsync(DateTime month)
    {
        var entry = await _db.MonthlyEntries.FirstOrDefaultAsync(e => e.Month == month);

        if (entry == null)
        {
            _logger.LogInformation("No entry found for {Month:yyyy-MM}", month);
            return 0;
        }

        return await CalculateAllocationsAsync(entry.Id);
    }

    /// <summary>
    /// Reverts each affected account's transfer accumulator to what it was before this monthly
    /// entry's allocations contributed to it. Call this before permanently deleting a monthly
    /// entry (whose allocations cascade-delete), so the accumulator doesn't retain a phantom
    /// contribution from a month that no longer exists. Only restores accounts for which this
    /// is still their most recent allocation, to avoid corrupting the chain if an older month
    /// is deleted out of the usual chronological order.
    /// </summary>
    public async Task UndoTransferAccumulatorContributionsAsync(int monthlyEntryId)
    {
        var allocations = await _db.SavingsAllocations
            .Include(a => a.SavingsAccount)
            .Include(a => a.MonthlyEntry)
            .Where(a => a.MonthlyEntryId == monthlyEntryId)
            .ToListAsync();

        foreach (var allocation in allocations)
        {
            var hasNewerAllocation = await _db.SavingsAllocations
                .AnyAsync(a => a.SavingsAccountId == allocation.SavingsAccountId
                    && a.MonthlyEntry.Month > allocation.MonthlyEntry.Month);

            if (!hasNewerAllocation)
                allocation.SavingsAccount.TransferAccumulator = allocation.AccumulatorBefore;
        }

        await _db.SaveChangesAsync();
    }
}
