namespace MySavings.ApiService.Models;

/// <summary>
/// Represents the allocation of a portion of savings to a specific account for a given month
/// Stores both the calculated amount and the rule parameters used at import time
/// </summary>
public class SavingsAllocation
{
    public int Id { get; set; }

    /// <summary>
    /// Monthly entry ID
    /// </summary>
    public int MonthlyEntryId { get; set; }
    public MonthlyEntry MonthlyEntry { get; set; } = null!;

    /// <summary>
    /// Savings account ID
    /// </summary>
    public int SavingsAccountId { get; set; }
    public SavingsAccount SavingsAccount { get; set; } = null!;

    /// <summary>
    /// Amount allocated to this account for this month
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Percentage of savings allocated (calculated)
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Weight used for this allocation (snapshot from Excel/allocation rule at import time)
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Indicates whether the transfer for this allocation has been executed.
    /// Initialized to true for months up to and including July 2026.
    /// </summary>
    public bool IsTransferred { get; set; }

    /// <summary>
    /// Amount actually worth transferring this month, after applying the account's
    /// TransferThreshold/accumulator logic. Equals Amount when the threshold is 0 or reached;
    /// 0 when the accumulated amount hasn't reached the threshold yet.
    /// </summary>
    public decimal TransferableAmount { get; set; }

    /// <summary>
    /// Snapshot of the account's TransferAccumulator immediately before this allocation's
    /// contribution was applied. Lets recalculation cleanly undo and redo this month's
    /// contribution without disturbing other months.
    /// </summary>
    public decimal AccumulatorBefore { get; set; }
}
