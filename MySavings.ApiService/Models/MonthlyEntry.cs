namespace MySavings.ApiService.Models;

/// <summary>
/// Represents a monthly entry for both persons
/// </summary>
public class MonthlyEntry
{
    public int Id { get; set; }

    /// <summary>
    /// Month and year (e.g., 2024-01-01 for January 2024)
    /// </summary>
    public DateTime Month { get; set; }

    /// <summary>
    /// Salary for Person1 for the month
    /// </summary>
    public decimal Person1Salary { get; set; }

    /// <summary>
    /// Expenses for Person1 for the month
    /// </summary>
    public decimal Person1Expenses { get; set; }

    /// <summary>
    /// Savings achieved by Person1 (Person1Salary - Person1Expenses)
    /// </summary>
    public decimal Person1Savings { get; set; }

    /// <summary>
    /// Person1 Savings/Salary ratio
    /// </summary>
    public decimal Person1SavingsRatio { get; set; }

    /// <summary>
    /// Salary for Person2 for the month
    /// </summary>
    public decimal Person2Salary { get; set; }

    /// <summary>
    /// Expenses for Person2 for the month
    /// </summary>
    public decimal Person2Expenses { get; set; }

    /// <summary>
    /// Savings achieved by Person2 (Person2Salary - Person2Expenses)
    /// </summary>
    public decimal Person2Savings { get; set; }

    /// <summary>
    /// Person2 Savings/Salary ratio
    /// </summary>
    public decimal Person2SavingsRatio { get; set; }

    /// <summary>
    /// Total combined salary (Person1Salary + Person2Salary)
    /// </summary>
    public decimal TotalSalary { get; set; }

    /// <summary>
    /// Total combined expenses (Person1Expenses + Person2Expenses)
    /// </summary>
    public decimal TotalExpenses { get; set; }

    /// <summary>
    /// Total combined savings (Person1Savings + Person2Savings)
    /// </summary>
    public decimal TotalSavings { get; set; }

    /// <summary>
    /// Total Savings/Salary ratio
    /// </summary>
    public decimal TotalSavingsRatio { get; set; }

    /// <summary>
    /// Percentage contribution of Person1 (Louis) from previous month (M-1)
    /// Used in allocation formula: Weight × (OwnerPercent_M-1 / 0.5) × TotalSavings
    /// </summary>
    public decimal PreviousMonthPerson1Percent { get; set; }

    /// <summary>
    /// Percentage contribution of Person2 (Alice) from previous month (M-1)
    /// Used in allocation formula: Weight × (OwnerPercent_M-1 / 0.5) × TotalSavings
    /// </summary>
    public decimal PreviousMonthPerson2Percent { get; set; }

    /// <summary>
    /// Allocations to different savings accounts for this month
    /// </summary>
    public List<SavingsAllocation> SavingsAllocations { get; set; } = new();
}
