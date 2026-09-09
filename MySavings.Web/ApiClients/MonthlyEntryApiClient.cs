namespace MySavings.Web.ApiClients;

public class MonthlyEntryApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<HttpResponseMessage> CreateAsync(CreateMonthlyEntryRequest request)
    {
        return await _httpClient.PostAsJsonAsync("/api/monthly-entries", request);
    }

    public async Task<HttpResponseMessage> DeleteAsync(int id)
    {
        return await _httpClient.DeleteAsync($"/api/monthly-entries/{id}");
    }
}

public class CreateMonthlyEntryRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Person1Salary { get; set; }
    public decimal Person2Salary { get; set; }
    public decimal EndMonthBeforeSalary { get; set; }
}
