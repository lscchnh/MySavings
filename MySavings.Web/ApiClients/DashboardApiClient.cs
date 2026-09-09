namespace MySavings.Web.ApiClients;

public class DashboardApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<DashboardSummaryDto?> GetDashboardSummaryAsync()
    {
        return await _httpClient.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");
    }

    public async Task<List<SummaryViewDto>> GetDashboardHistoryAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<SummaryViewDto>>("/api/dashboard/history") ?? new();
    }

    public async Task SetAllocationTransferredAsync(int allocationId, bool isTransferred)
    {
        await _httpClient.PatchAsJsonAsync($"/api/savings-allocations/{allocationId}/transferred", new { IsTransferred = isTransferred });
    }
}

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

public class SummaryViewDto
{
    public int Id { get; set; }
    public DateTime Month { get; set; }
    public decimal Person1Salary { get; set; }
    public decimal Person2Salary { get; set; }
    public decimal TotalSalary { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSavings { get; set; }
    public decimal TotalSavingsRatio { get; set; }
    public decimal Person1ContributionPercent { get; set; }
    public decimal Person2ContributionPercent { get; set; }
    public List<AllocationDto> Allocations { get; set; } = new();
}

public class AllocationDto
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public decimal Amount { get; set; }
    public decimal Weight { get; set; }
    public bool IsTransferred { get; set; }
    public decimal TransferableAmount { get; set; }
}
