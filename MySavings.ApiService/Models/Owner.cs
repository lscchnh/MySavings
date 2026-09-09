namespace MySavings.ApiService.Models;

public class Owner
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Target share (0-1) of total savings this owner's accounts should receive.
    /// The sum across all owners must equal 1. Defaults to an even split.
    /// </summary>
    public decimal AllocationSharePercent { get; set; } = 0.5m;

    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = new List<SavingsAccount>();
}
