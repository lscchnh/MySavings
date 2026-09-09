namespace MySavings.ApiService.Models;

/// <summary>
/// A named group of savings accounts that are transferred together in one bank transfer.
/// </summary>
public class TransferGroup
{
    public int Id { get; set; }

    /// <summary>Label shown in the "Virements à effectuer" section.</summary>
    public string Name { get; set; } = string.Empty;

    public ICollection<SavingsAccount> SavingsAccounts { get; set; } = new List<SavingsAccount>();
}
