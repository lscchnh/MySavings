namespace MySavings.Web.ApiClients;

public class TransferGroupApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<List<TransferGroupDto>> GetAllAsync()
        => await _httpClient.GetFromJsonAsync<List<TransferGroupDto>>("/api/transfer-groups") ?? new();

    public async Task<TransferGroupDto?> CreateAsync(CreateTransferGroupRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/transfer-groups", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TransferGroupDto>();
    }

    public async Task<TransferGroupDto?> UpdateAsync(int id, UpdateTransferGroupRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/transfer-groups/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TransferGroupDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/transfer-groups/{id}");
        response.EnsureSuccessStatusCode();
    }
}

public class TransferGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TransferGroupAccountDto> Accounts { get; set; } = new();
}

public class TransferGroupAccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public record CreateTransferGroupRequest(string Name, List<string> AccountNames);
public record UpdateTransferGroupRequest(string Name, List<string> AccountNames);
