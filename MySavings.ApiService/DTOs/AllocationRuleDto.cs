using MySavings.ApiService.Models;

namespace MySavings.ApiService.DTOs;

public class BulkUpdateAllocationRuleRequest
{
    public int SavingsAccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal Weight { get; set; }
    public int? OwnerId { get; set; }
    public LiquidityLevel? LiquidityLevel { get; set; }
    public decimal? TransferThreshold { get; set; }
    public decimal? TransferAccumulator { get; set; }
}

public class AllocationRuleWithAccountDto
{
    public int SavingsAccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public decimal Weight { get; set; }
    public LiquidityLevel LiquidityLevel { get; set; }
    public decimal TransferThreshold { get; set; }
    public decimal TransferAccumulator { get; set; }
}

public class CreateSavingsAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public LiquidityLevel LiquidityLevel { get; set; } = LiquidityLevel.Liquide;
    public decimal TransferThreshold { get; set; } = 0m;
}

public class UpdateOwnerShareRequest
{
    public int OwnerId { get; set; }
    public decimal SharePercent { get; set; }
}
