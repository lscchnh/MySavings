namespace MySavings.ApiService.Models;

/// <summary>
/// Represents a savings account/envelope (PEE, PER, AV, PEA, CTO, Livret A, etc.)
/// </summary>
public class SavingsAccount
{
    public int Id { get; set; }

    /// <summary>
    /// Full name of the account (unique identifier)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Owner ID (FK to Owner table). Null for shared accounts.
    /// 1 = Person1, 2 = Person2
    /// </summary>
    public int? OwnerId { get; set; }

    /// <summary>
    /// How quickly this account's funds can realistically be accessed.
    /// </summary>
    public LiquidityLevel LiquidityLevel { get; set; } = LiquidityLevel.Liquide;

    /// <summary>
    /// Minimum amount required for a transfer to this account to be worth doing (0 = no minimum).
    /// </summary>
    public decimal TransferThreshold { get; set; } = 0m;

    /// <summary>
    /// Amount carried forward from months where the calculated share didn't reach
    /// TransferThreshold. Added to the next month's share until the threshold is reached,
    /// at which point the full accumulated amount becomes transferable and this resets to 0.
    /// Unused (stays 0) when TransferThreshold is 0.
    /// </summary>
    public decimal TransferAccumulator { get; set; } = 0m;

    /// <summary>
    /// Navigation property to allocation rule
    /// </summary>
    public AllocationRule? AllocationRule { get; set; }

    public ICollection<TransferGroup> TransferGroups { get; set; } = new List<TransferGroup>();
}
