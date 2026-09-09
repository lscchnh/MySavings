namespace MySavings.Web.ApiClients;

public class PersonSettingsApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<List<PersonSettingsDto>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<PersonSettingsDto>>("/api/person-settings") ?? new();
    }

    public async Task<PersonSettingsDto?> UpdateAsync(int id, UpdatePersonSettingsRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/person-settings/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PersonSettingsDto>();
    }
}

public class PersonSettingsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public decimal AllocationSharePercent { get; set; }
}

public class UpdatePersonSettingsRequest
{
    public string Name { get; set; } = string.Empty;
}
