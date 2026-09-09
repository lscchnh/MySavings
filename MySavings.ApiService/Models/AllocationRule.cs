namespace MySavings.ApiService.Models;

/// <summary>
/// Allocation rule for a specific savings account
/// Defines how savings should be distributed
/// </summary>
public class AllocationRule
{
    public int Id { get; set; }

    /// <summary>
    /// ID of the concerned savings account
    /// </summary>
    public int SavingsAccountId { get; set; }
    public SavingsAccount SavingsAccount { get; set; } = null!;

    /// <summary>
    /// Weight for automatic allocation (e.g., 0.05, 0.1, 0.2)
    /// </summary>
    public decimal Weight { get; set; }
}
