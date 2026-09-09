namespace MySavings.ApiService.DTOs;

/// <summary>
/// Consolidated view for Person 1 + Person 2
/// </summary>
public class ConsolidatedViewDto
{
    public int Id { get; set; }
    public DateTime Month { get; set; }

    public decimal Person1Salary { get; set; }
    public decimal Person1Expenses { get; set; }
    public decimal Person1Savings { get; set; }
    public decimal Person1SavingsRatio { get; set; }

    public decimal Person2Salary { get; set; }
    public decimal Person2Expenses { get; set; }
    public decimal Person2Savings { get; set; }
    public decimal Person2SavingsRatio { get; set; }

    public decimal TotalSalary { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSavings { get; set; }
    public decimal TotalSavingsRatio { get; set; }

    public decimal Person1ContributionPercent { get; set; }
    public decimal Person2ContributionPercent { get; set; }

    public List<ConsolidatedAllocationDto> Allocations { get; set; } = new();
}

public class ConsolidatedAllocationDto
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public decimal Amount { get; set; }
    public decimal Weight { get; set; }
    public decimal Percentage { get; set; }
    public bool IsTransferred { get; set; }
    public decimal TransferableAmount { get; set; }
}

/// <summary>
/// Summary for the dashboard
/// </summary>
public class DashboardSummaryDto
{
    public DateTime CurrentMonth { get; set; }
    public decimal TotalSalary { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSavings { get; set; }
    public decimal SavingsRatio { get; set; }

    public List<MonthlyTrendDto> Months { get; set; } = new();
}

public class MonthlyTrendDto
{
    public DateTime Month { get; set; }
    public decimal Salary { get; set; }
    public decimal Expenses { get; set; }
    public decimal Savings { get; set; }
    public decimal SavingsRatio { get; set; }
}
