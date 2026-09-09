namespace MySavings.Web.ApiClients;

public class AllocationRuleApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<AllocationRulesWithAccountsResponse> GetWithAccountsAsync()
    {
        return await _httpClient.GetFromJsonAsync<AllocationRulesWithAccountsResponse>("/api/allocation-rules/with-accounts") ?? new();
    }

    public async Task<HttpResponseMessage> BulkUpdateAsync(List<BulkUpdateAllocationRuleRequest> requests)
    {
        return await _httpClient.PutAsJsonAsync("/api/allocation-rules/bulk", requests);
    }

    public async Task<HttpResponseMessage> UpdateOwnerSharesAsync(List<UpdateOwnerShareRequest> requests)
    {
        return await _httpClient.PutAsJsonAsync("/api/allocation-rules/owner-shares", requests);
    }

    public async Task<HttpResponseMessage> CreateSavingsAccountAsync(CreateSavingsAccountRequest request)
    {
        return await _httpClient.PostAsJsonAsync("/api/savings-accounts", request);
    }

    public async Task<HttpResponseMessage> DeleteSavingsAccountAsync(int savingsAccountId)
    {
        return await _httpClient.DeleteAsync($"/api/savings-accounts/{savingsAccountId}");
    }
}

public class AllocationRulesWithAccountsResponse
{
    public List<AllocationRuleWithAccountDto> Rules { get; set; } = [];
    public decimal TotalWeightPercent { get; set; }
}

public class AllocationRuleWithAccountDto
{
    public int SavingsAccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public decimal Weight { get; set; }
    public string LiquidityLevel { get; set; } = "Liquide";
    public decimal TransferThreshold { get; set; }
    public decimal TransferAccumulator { get; set; }
}

public class BulkUpdateAllocationRuleRequest
{
    public int SavingsAccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal Weight { get; set; }
    public int? OwnerId { get; set; }
    public string? LiquidityLevel { get; set; }
    public decimal? TransferThreshold { get; set; }
    public decimal? TransferAccumulator { get; set; }
}

public class CreateSavingsAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public string LiquidityLevel { get; set; } = "Liquide";
    public decimal TransferThreshold { get; set; } = 0m;
}

public class UpdateOwnerShareRequest
{
    public int OwnerId { get; set; }
    public decimal SharePercent { get; set; }
}
