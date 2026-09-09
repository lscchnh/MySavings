namespace MySavings.Web.ApiClients;

public class DatabaseApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<HttpResponseMessage> ClearDatabaseAsync()
    {
        return await _httpClient.DeleteAsync("/api/database/clear");
    }
}
