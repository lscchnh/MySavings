namespace MySavings.Web.ApiClients;

public class AppSettingsApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<AppSettingsDto> GetAsync()
    {
        return await _httpClient.GetFromJsonAsync<AppSettingsDto>("/api/app-settings") ?? new();
    }

    public async Task<HttpResponseMessage> UpdateAsync(UpdateAppSettingsRequest request)
    {
        return await _httpClient.PutAsJsonAsync("/api/app-settings", request);
    }
}

public class AppSettingsDto
{
    public decimal SecurityBuffer { get; set; }
}

public class UpdateAppSettingsRequest
{
    public decimal SecurityBuffer { get; set; }
}
