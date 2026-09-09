using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MySavings.ApiService.DTOs;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class AllocationRuleEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public AllocationRuleEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    // ── POST /api/savings-accounts ──────────────────────────────────────

    [Fact]
    public async Task CreateAccount_ValidRequest_ReturnsCreated()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        var response = await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "PEA Louis", OwnerId = 7 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var db = _factory.CreateDbContext();
        var account = Assert.Single(db.SavingsAccounts);
        Assert.Equal("PEA Louis", account.Name);
        Assert.Equal(7, account.OwnerId);
    }

    [Fact]
    public async Task CreateAccount_WithLiquidityLevel_PersistsIt()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        var response = await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "Livret A Louis", OwnerId = 7, LiquidityLevel = LiquidityLevel.MoyenTerme });

        response.EnsureSuccessStatusCode();

        await using var db = _factory.CreateDbContext();
        var account = Assert.Single(db.SavingsAccounts);
        Assert.Equal(LiquidityLevel.MoyenTerme, account.LiquidityLevel);
    }

    [Fact]
    public async Task CreateAccount_NoLiquidityLevelSpecified_DefaultsToLiquide()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "PEA Louis", OwnerId = 7 });

        await using var db = _factory.CreateDbContext();
        var account = Assert.Single(db.SavingsAccounts);
        Assert.Equal(LiquidityLevel.Liquide, account.LiquidityLevel);
    }

    [Fact]
    public async Task CreateAccount_WithTransferThreshold_PersistsIt()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        var response = await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "Assu Vie Fortuneo Louis", OwnerId = 7, TransferThreshold = 100m });

        response.EnsureSuccessStatusCode();

        await using var db = _factory.CreateDbContext();
        var account = Assert.Single(db.SavingsAccounts);
        Assert.Equal(100m, account.TransferThreshold);
        Assert.Equal(0m, account.TransferAccumulator);
    }

    [Fact]
    public async Task CreateAccount_NegativeTransferThreshold_ReturnsBadRequest()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        var response = await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "PEA Louis", OwnerId = 7, TransferThreshold = -50m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_MissingName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "   ", OwnerId = 7 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_DuplicateName_ReturnsConflict()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.SavingsAccounts.Add(new SavingsAccount { Name = "PEA Louis", OwnerId = 7 });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/savings-accounts",
            new CreateSavingsAccountRequest { Name = "PEA Louis", OwnerId = 7 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── DELETE /api/savings-accounts/{id} ───────────────────────────────

    [Fact]
    public async Task DeleteAccount_NotFound_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/savings-accounts/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ExistingAccount_RemovesAccountRuleAndAllocations()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;

            seedCtx.AllocationRules.Add(new AllocationRule { SavingsAccountId = accountId, Weight = 0.5m });

            var entry = new MonthlyEntry { Month = new DateTime(2024, 1, 1), TotalSalary = 1000m, TotalSavings = 500m };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entry.Id,
                SavingsAccountId = accountId,
                Amount = 250m,
                Percentage = 0.5m,
                Weight = 0.5m
            });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/savings-accounts/{accountId}");
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.SavingsAccounts);
        Assert.Empty(verifyCtx.AllocationRules);
        Assert.Empty(verifyCtx.SavingsAllocations);
    }

    // ── GET /api/allocation-rules/with-accounts ─────────────────────────

    [Fact]
    public async Task GetRulesWithAccounts_ReturnsAccountsWithWeightsAndTotalWeight()
    {
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            var pea = new SavingsAccount { Name = "PEA Louis", OwnerId = 7, LiquidityLevel = LiquidityLevel.LongTerme };
            var ldd = new SavingsAccount { Name = "LDD Alice", OwnerId = 8 };
            seedCtx.SavingsAccounts.AddRange(pea, ldd);
            await seedCtx.SaveChangesAsync();

            seedCtx.AllocationRules.Add(new AllocationRule { SavingsAccountId = pea.Id, Weight = 0.3m });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/allocation-rules/with-accounts");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rules = body.GetProperty("rules").EnumerateArray().ToList();

        Assert.Equal(2, rules.Count);
        Assert.Equal(0.3m, body.GetProperty("totalWeight").GetDecimal());
        Assert.Equal(30m, body.GetProperty("totalWeightPercent").GetDecimal());

        var lddRule = rules.Single(r => r.GetProperty("accountName").GetString() == "LDD Alice");
        Assert.Equal(0m, lddRule.GetProperty("weight").GetDecimal());
        Assert.Equal("Liquide", lddRule.GetProperty("liquidityLevel").GetString());

        var peaRule = rules.Single(r => r.GetProperty("accountName").GetString() == "PEA Louis");
        Assert.Equal("LongTerme", peaRule.GetProperty("liquidityLevel").GetString());
    }

    [Fact]
    public async Task GetRulesWithAccounts_IncludesTransferThresholdAndAccumulator()
    {
        await _factory.SeedOwnersAsync((7, "Louis"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.SavingsAccounts.Add(new SavingsAccount
            {
                Name = "Assu Vie Fortuneo Louis",
                OwnerId = 7,
                TransferThreshold = 100m,
                TransferAccumulator = 60m
            });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/allocation-rules/with-accounts");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rule = Assert.Single(body.GetProperty("rules").EnumerateArray().ToList());

        Assert.Equal(100m, rule.GetProperty("transferThreshold").GetDecimal());
        Assert.Equal(60m, rule.GetProperty("transferAccumulator").GetDecimal());
    }

    [Fact]
    public async Task BulkUpdate_WithLiquidityLevel_UpdatesAccountLiquidityLevel()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"));
        await _factory.SetOwnerShareAsync(7, 0.5m);

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "Livret A Louis", OwnerId = 7, LiquidityLevel = LiquidityLevel.Liquide };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 0.5m, OwnerId = 7, LiquidityLevel = LiquidityLevel.MoyenTerme }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        var updatedAccount = await verifyCtx.SavingsAccounts.FindAsync(accountId);
        Assert.Equal(LiquidityLevel.MoyenTerme, updatedAccount!.LiquidityLevel);
    }

    // ── PUT /api/allocation-rules/bulk ──────────────────────────────────

    [Fact]
    public async Task BulkUpdate_WithThresholdAndAccumulator_UpdatesAccount()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"));
        await _factory.SetOwnerShareAsync(7, 0.5m);

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "Assu Vie Fortuneo Louis", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 0.5m, OwnerId = 7, TransferThreshold = 100m, TransferAccumulator = 25m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        var updatedAccount = await verifyCtx.SavingsAccounts.FindAsync(accountId);
        Assert.Equal(100m, updatedAccount!.TransferThreshold);
        Assert.Equal(25m, updatedAccount.TransferAccumulator);
    }

    [Fact]
    public async Task BulkUpdate_NegativeTransferThreshold_ReturnsBadRequest()
    {
        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = 1, Weight = 0.5m, TransferThreshold = -10m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_NegativeTransferAccumulator_ReturnsBadRequest()
    {
        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = 1, Weight = 0.5m, TransferAccumulator = -10m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_WeightOutOfRange_ReturnsBadRequest()
    {
        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = 1, Weight = 1.5m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_PositiveWeightOnOwnerlessAccount_ReturnsBadRequest()
    {
        // A shared (ownerless) account has no target share to validate against, so a positive
        // weight for it must be rejected rather than silently skipped from the per-owner totals
        // (which would let the true overall weight exceed 100% undetected).
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"));
        await _factory.SetOwnerShareAsync(7, 1.0m);

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "Compte partagé", OwnerId = null };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 0.5m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.AllocationRules);
    }

    [Fact]
    public async Task BulkUpdate_SumExceeds100Percent_ReturnsBadRequest()
    {
        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = 1, Weight = 0.6m },
            new() { SavingsAccountId = 2, Weight = 0.6m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_ValidRequest_CreatesRuleAndUpdatesAccountOwnerAndName()
    {
        // Clear the migration-seeded owners so only 7/8 are checked by the per-owner
        // weight-sum validation, and set their target shares to match this test's scenario
        // (all weight ends up on owner 8 after the reassignment below).
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));
        await _factory.SetOwnerShareAsync(7, 0m);
        await _factory.SetOwnerShareAsync(8, 0.4m);

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 0.4m, OwnerId = 8, AccountName = "PEA Renamed" }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        var rule = Assert.Single(verifyCtx.AllocationRules);
        Assert.Equal(0.4m, rule.Weight);

        var updatedAccount = await verifyCtx.SavingsAccounts.FindAsync(accountId);
        Assert.Equal("PEA Renamed", updatedAccount!.Name);
        Assert.Equal(8, updatedAccount.OwnerId);
    }

    [Fact]
    public async Task BulkUpdate_ZeroWeight_RemovesExistingRule()
    {
        // Clear the migration-seeded owners and target owner 7's share at 0%, matching
        // the zero-weight submission below (rule removal).
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"));
        await _factory.SetOwnerShareAsync(7, 0m);

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;

            seedCtx.AllocationRules.Add(new AllocationRule { SavingsAccountId = accountId, Weight = 0.5m });
            await seedCtx.SaveChangesAsync();
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 0m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.AllocationRules);
    }

    [Fact]
    public async Task BulkUpdate_OwnerWeightSumDoesNotMatchShare_ReturnsBadRequest()
    {
        // Both owners default to a 50% share (from the migration / model default). Submitting
        // a single rule that gives owner 7 100% of the weight (and owner 8 the seeded default
        // account with no rule, i.e. 0%) should be rejected.
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 1.0m, OwnerId = 7 }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.AllocationRules);
    }

    [Fact]
    public async Task BulkUpdate_OwnerWeightSumMatchesCustomShare_Succeeds()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));
        await _factory.SetOwnerShareAsync(7, 0.7m);
        await _factory.SetOwnerShareAsync(8, 0.3m);

        int louisAccountId;
        int aliceAccountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var louisAccount = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            var aliceAccount = new SavingsAccount { Name = "PEA Alice", OwnerId = 8 };
            seedCtx.SavingsAccounts.AddRange(louisAccount, aliceAccount);
            await seedCtx.SaveChangesAsync();
            louisAccountId = louisAccount.Id;
            aliceAccountId = aliceAccount.Id;
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = louisAccountId, Weight = 0.7m, OwnerId = 7 },
            new() { SavingsAccountId = aliceAccountId, Weight = 0.3m, OwnerId = 8 }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);

        response.EnsureSuccessStatusCode();
    }

    // ── PUT /api/allocation-rules/owner-shares ──────────────────────────

    [Fact]
    public async Task UpdateOwnerShares_ValidRequest_UpdatesBothOwners()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        var requests = new List<UpdateOwnerShareRequest>
        {
            new() { OwnerId = 7, SharePercent = 0.6m },
            new() { OwnerId = 8, SharePercent = 0.4m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/owner-shares", requests);
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Equal(0.6m, (await verifyCtx.Owners.FindAsync(7))!.AllocationSharePercent);
        Assert.Equal(0.4m, (await verifyCtx.Owners.FindAsync(8))!.AllocationSharePercent);
    }

    [Fact]
    public async Task UpdateOwnerShares_SumNotEqual100_ReturnsBadRequest()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        var requests = new List<UpdateOwnerShareRequest>
        {
            new() { OwnerId = 7, SharePercent = 0.6m },
            new() { OwnerId = 8, SharePercent = 0.6m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/owner-shares", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Equal(0.5m, (await verifyCtx.Owners.FindAsync(7))!.AllocationSharePercent);
    }

    [Fact]
    public async Task UpdateOwnerShares_UnknownOwnerId_ReturnsBadRequest()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"));

        var requests = new List<UpdateOwnerShareRequest>
        {
            new() { OwnerId = 7, SharePercent = 0.6m },
            new() { OwnerId = 999, SharePercent = 0.4m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/owner-shares", requests);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Saving a répartition change only applies going forward, never retroactively ──

    [Fact]
    public async Task BulkUpdate_SavingNewRule_DoesNotRetroactivelyRecalculateExistingMonths()
    {
        // Owner ids don't start at 1/2 here on purpose: the migration always seeds a default
        // Louis(1)/Alice(2) pair, so clearing first proves the validation matches owners by
        // ordinal position (lowest Id = Person1) rather than the literal id value.
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"));
        await _factory.SetOwnerShareAsync(7, 0.5m);

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;

            seedCtx.MonthlyEntries.Add(new MonthlyEntry
            {
                Month = new DateTime(2024, 1, 1),
                TotalSalary = 1000m,
                TotalSavings = 1000m,
                PreviousMonthPerson1Percent = 0.5m,
                PreviousMonthPerson2Percent = 0.5m
            });
            await seedCtx.SaveChangesAsync();
        }

        var requests = new List<BulkUpdateAllocationRuleRequest>
        {
            new() { SavingsAccountId = accountId, Weight = 0.5m, OwnerId = 7 }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/bulk", requests);
        response.EnsureSuccessStatusCode();

        // The rule now exists, but the already-recorded January month is left untouched:
        // rule changes only take effect for the next monthly entry created/edited.
        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Single(verifyCtx.AllocationRules);
        Assert.Empty(verifyCtx.SavingsAllocations);
    }

    [Fact]
    public async Task UpdateOwnerShares_DoesNotRetroactivelyRecalculateExistingMonths()
    {
        await _factory.ClearOwnersAsync();
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        int accountId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA", OwnerId = 7 };
            seedCtx.SavingsAccounts.Add(account);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;

            seedCtx.AllocationRules.Add(new AllocationRule { SavingsAccountId = accountId, Weight = 0.5m });

            var entry = new MonthlyEntry
            {
                Month = new DateTime(2024, 1, 1),
                TotalSalary = 1000m,
                TotalSavings = 1000m,
                PreviousMonthPerson1Percent = 0.5m,
                PreviousMonthPerson2Percent = 0.5m
            };
            seedCtx.MonthlyEntries.Add(entry);
            await seedCtx.SaveChangesAsync();

            seedCtx.SavingsAllocations.Add(new SavingsAllocation
            {
                MonthlyEntryId = entry.Id,
                SavingsAccountId = accountId,
                Amount = 500m,
                Percentage = 0.5m,
                Weight = 0.5m
            });
            await seedCtx.SaveChangesAsync();
        }

        // Change the shares to something inconsistent with the existing (unrelated) rule weight -
        // if this triggered a recalculation, the amount below would no longer equal 500.
        var requests = new List<UpdateOwnerShareRequest>
        {
            new() { OwnerId = 7, SharePercent = 0.7m },
            new() { OwnerId = 8, SharePercent = 0.3m }
        };

        var response = await _client.PutAsJsonAsync("/api/allocation-rules/owner-shares", requests);
        response.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        var allocation = Assert.Single(verifyCtx.SavingsAllocations);
        Assert.Equal(500m, allocation.Amount);
    }
}
