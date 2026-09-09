using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.DTOs;
using MySavings.ApiService.Models;
using MySavings.ApiService.Services;

namespace MySavings.ApiService.Endpoints;

public static class AllocationRuleEndpoints
{
    public static void MapAllocationRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/allocation-rules")
            .WithTags("Allocation Rules");

        // PUT update all rules at once (bulk update)
        group.MapPut("/bulk", async (
            List<BulkUpdateAllocationRuleRequest> requests,
            MySavingsDbContext db) =>
        {
            // Validation: each weight must be between 0 and 1
            if (requests.Any(r => r.Weight < 0 || r.Weight > 1))
                return Results.BadRequest(new { error = "Chaque pondération doit être comprise entre 0 et 1." });

            if (requests.Any(r => r.TransferThreshold is < 0))
                return Results.BadRequest(new { error = "Le seuil de virement ne peut pas être négatif." });

            if (requests.Any(r => r.TransferAccumulator is < 0))
                return Results.BadRequest(new { error = "L'accumulateur ne peut pas être négatif." });

            decimal totalWeight = requests.Sum(r => r.Weight);

            var accountIds = requests.Select(r => r.SavingsAccountId).ToList();

            var rulesByAccountId = await db.AllocationRules
                .Where(r => accountIds.Contains(r.SavingsAccountId))
                .ToDictionaryAsync(r => r.SavingsAccountId);

            var accountsById = await db.SavingsAccounts
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);

            // Validation: for each owner, the sum of their accounts' weights must match
            // that owner's target allocation share (e.g. Louis 60% / Alice 40%).
            var owners = await db.Owners.ToListAsync();
            var weightByOwnerId = new Dictionary<int, decimal>();
            foreach (var request in requests)
            {
                if (request.Weight <= 0)
                    continue;

                int? ownerId = request.OwnerId ?? (accountsById.TryGetValue(request.SavingsAccountId, out var existingAccount) ? existingAccount.OwnerId : null);
                if (ownerId is null)
                {
                    // A shared (ownerless) account can't have a target share to validate against,
                    // so it must not silently carry weight past the per-owner checks below.
                    return Results.BadRequest(new
                    {
                        error = "Un compte sans propriétaire ne peut pas avoir de pondération. Assignez-lui un propriétaire."
                    });
                }

                weightByOwnerId[ownerId.Value] = weightByOwnerId.GetValueOrDefault(ownerId.Value) + request.Weight;
            }

            const decimal ownerShareTolerance = 0.0001m;
            foreach (var owner in owners)
            {
                var ownerWeight = weightByOwnerId.GetValueOrDefault(owner.Id);
                if (Math.Abs(ownerWeight - owner.AllocationSharePercent) > ownerShareTolerance)
                {
                    return Results.BadRequest(new
                    {
                        error = $"La somme des pondérations de {owner.Name} ({(ownerWeight * 100):N2}%) doit être égale à sa répartition d'allocation ({(owner.AllocationSharePercent * 100):N2}%)."
                    });
                }
            }

            foreach (var request in requests)
            {
                rulesByAccountId.TryGetValue(request.SavingsAccountId, out var rule);

                if (request.Weight > 0)
                {
                    if (rule == null)
                    {
                        rule = new AllocationRule
                        {
                            SavingsAccountId = request.SavingsAccountId,
                            Weight = request.Weight
                        };
                        db.AllocationRules.Add(rule);
                    }
                    else
                    {
                        rule.Weight = request.Weight;
                    }
                }
                else if (rule != null)
                {
                    db.AllocationRules.Remove(rule);
                }

                if (accountsById.TryGetValue(request.SavingsAccountId, out var account))
                {
                    if (request.OwnerId.HasValue)
                        account.OwnerId = request.OwnerId.Value;
                    if (!string.IsNullOrEmpty(request.AccountName))
                        account.Name = request.AccountName;
                    if (request.LiquidityLevel.HasValue)
                        account.LiquidityLevel = request.LiquidityLevel.Value;
                    if (request.TransferThreshold.HasValue)
                        account.TransferThreshold = request.TransferThreshold.Value;
                    if (request.TransferAccumulator.HasValue)
                        account.TransferAccumulator = request.TransferAccumulator.Value;
                }
            }

            await db.SaveChangesAsync();

            // Intentionally not recalculating past months: rule changes only take effect for
            // the next monthly entry created/edited, so historical allocations stay untouched.
            return Results.Ok(new
            {
                message = "Règles mises à jour avec succès. Le changement s'appliquera à partir du prochain mois enregistré ; les mois déjà enregistrés ne sont pas modifiés.",
                totalWeightPercent = totalWeight * 100
            });
        })
        .WithName("BulkUpdateAllocationRules");

        // GET rules with accounts info (for bulk editing)
        group.MapGet("/with-accounts", async (MySavingsDbContext db) =>
        {
            var data = await db.SavingsAccounts
                .Include(a => a.AllocationRule)
                .OrderBy(a => a.OwnerId)
                .ThenBy(a => a.Name)
                .Select(a => new AllocationRuleWithAccountDto
                {
                    SavingsAccountId = a.Id,
                    AccountName = a.Name,
                    OwnerId = a.OwnerId,
                    Weight = a.AllocationRule != null ? a.AllocationRule.Weight : 0,
                    LiquidityLevel = a.LiquidityLevel,
                    TransferThreshold = a.TransferThreshold,
                    TransferAccumulator = a.TransferAccumulator
                })
                .ToListAsync();

            decimal totalWeight = data.Sum(d => d.Weight);

            return Results.Ok(new
            {
                rules = data,
                totalWeight,
                totalWeightPercent = totalWeight * 100
            });
        })
        .WithName("GetAllocationRulesWithAccounts");

        // PUT update owners' target allocation shares (e.g. Louis 60% / Alice 40%)
        group.MapPut("/owner-shares", async (
            List<UpdateOwnerShareRequest> requests,
            MySavingsDbContext db) =>
        {
            if (requests.Any(r => r.SharePercent < 0 || r.SharePercent > 1))
                return Results.BadRequest(new { error = "Chaque répartition doit être comprise entre 0 et 100%." });

            var ownerIds = requests.Select(r => r.OwnerId).ToList();
            var owners = await db.Owners.Where(o => ownerIds.Contains(o.Id)).ToListAsync();

            if (owners.Count != requests.Count)
                return Results.BadRequest(new { error = "Un ou plusieurs propriétaires sont introuvables." });

            decimal totalShare = requests.Sum(r => r.SharePercent);
            const decimal shareTolerance = 0.0001m;
            if (Math.Abs(totalShare - 1.0m) > shareTolerance)
                return Results.BadRequest(new { error = $"La somme des répartitions ({(totalShare * 100):N2}%) doit être égale à 100%." });

            foreach (var request in requests)
            {
                var owner = owners.First(o => o.Id == request.OwnerId);
                owner.AllocationSharePercent = request.SharePercent;
                owner.LastModified = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            // Intentionally not recalculating past months: the new shares only constrain how
            // rule weights are assigned going forward, they don't retroactively change history.
            return Results.Ok(owners.Select(o => new PersonSettingsDto
            {
                Id = o.Id,
                Name = o.Name,
                LastModified = o.LastModified,
                AllocationSharePercent = o.AllocationSharePercent
            }));
        })
        .WithName("UpdateOwnerShares");

        // ── Savings Accounts ─────────────────────────────────────────────────
        var accountsGroup = app.MapGroup("/api/savings-accounts")
            .WithTags("Savings Accounts");

        // POST create a new savings account
        accountsGroup.MapPost("/", async (
            CreateSavingsAccountRequest request,
            MySavingsDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Le nom du compte est obligatoire." });

            if (request.TransferThreshold < 0)
                return Results.BadRequest(new { error = "Le seuil de virement ne peut pas être négatif." });

            var normalizedName = request.Name.Trim();
            var alreadyExists = await db.SavingsAccounts
                .AnyAsync(a => a.Name == normalizedName);
            if (alreadyExists)
                return Results.Conflict(new { error = $"Un compte avec le nom '{normalizedName}' existe déjà." });

            var account = new Models.SavingsAccount
            {
                Name = normalizedName,
                OwnerId = request.OwnerId,
                LiquidityLevel = request.LiquidityLevel,
                TransferThreshold = request.TransferThreshold
            };

            db.SavingsAccounts.Add(account);
            await db.SaveChangesAsync();

            return Results.Created($"/api/savings-accounts/{account.Id}", new
            {
                account.Id,
                account.Name,
                account.OwnerId,
                account.LiquidityLevel,
                account.TransferThreshold
            });
        })
        .WithName("CreateSavingsAccount");

        // DELETE a savings account and related data
        accountsGroup.MapDelete("/{id:int}", async (
            int id,
            MySavingsDbContext db,
            AllocationCalculationService allocationService,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(AllocationRuleEndpoints));

            var account = await db.SavingsAccounts
                .Include(a => a.TransferGroups)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (account is null)
                return Results.NotFound(new { error = "Compte introuvable." });

            var relatedAllocations = await db.SavingsAllocations
                .Where(a => a.SavingsAccountId == id)
                .ToListAsync();
            if (relatedAllocations.Count != 0)
                db.SavingsAllocations.RemoveRange(relatedAllocations);

            var rule = await db.AllocationRules.FirstOrDefaultAsync(r => r.SavingsAccountId == id);
            if (rule != null)
                db.AllocationRules.Remove(rule);

            // Remove from transfer groups (many-to-many)
            account.TransferGroups.Clear();

            db.SavingsAccounts.Remove(account);
            await db.SaveChangesAsync();

            await RecalculateAllMonthsAsync(db, allocationService, logger, "account deletion");

            return Results.Ok(new { message = "Compte supprimé avec succès." });
        })
        .WithName("DeleteSavingsAccount");
    }

    /// <summary>
    /// Recalculates allocations for every month with data, in chronological order. Used when an
    /// account is deleted, since its allocations must be purged from history entirely (unlike
    /// rule/share edits, which intentionally only affect future monthly entries). Chronological
    /// order matters here: each account's transfer-threshold accumulator carries forward from
    /// one month to the next, so recalculating out of order would corrupt it.
    /// </summary>
    private static async Task RecalculateAllMonthsAsync(
        MySavingsDbContext db,
        AllocationCalculationService allocationService,
        ILogger logger,
        string context)
    {
        var months = await db.MonthlyEntries
            .Select(e => e.Month)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        foreach (var month in months)
        {
            try
            {
                await allocationService.CalculateAllocationsForMonthAsync(month);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error recalculating allocations for {Month:yyyy-MM} after {Context}", month, context);
            }
        }
    }
}
