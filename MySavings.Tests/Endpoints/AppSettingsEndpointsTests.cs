using System.Net;
using System.Net.Http.Json;
using MySavings.ApiService.Endpoints;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class AppSettingsEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public AppSettingsEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_FreshDatabase_ReturnsDefaultSecurityBufferOf500()
    {
        var settings = await _client.GetFromJsonAsync<AppSettingsDto>("/api/app-settings");

        Assert.NotNull(settings);
        Assert.Equal(500m, settings!.SecurityBuffer);
    }

    [Fact]
    public async Task Update_ValidRequest_UpdatesAndPersistsValue()
    {
        var response = await _client.PutAsJsonAsync("/api/app-settings", new UpdateAppSettingsRequest { SecurityBuffer = 750m });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<AppSettingsDto>();
        Assert.Equal(750m, dto!.SecurityBuffer);

        var refetched = await _client.GetFromJsonAsync<AppSettingsDto>("/api/app-settings");
        Assert.Equal(750m, refetched!.SecurityBuffer);
    }

    [Fact]
    public async Task Update_WhenNoRowExists_CreatesExactlyOneRowWithFixedId()
    {
        // Simulates two "first save" requests both finding no row yet. Previously this used
        // FirstOrDefaultAsync + autoincrement, so both could insert distinct rows; the fixed
        // singleton id (1) means the second save updates the same row instead of duplicating it.
        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.AppSettings.RemoveRange(seedCtx.AppSettings);
            await seedCtx.SaveChangesAsync();
        }

        var response1 = await _client.PutAsJsonAsync("/api/app-settings", new UpdateAppSettingsRequest { SecurityBuffer = 300m });
        response1.EnsureSuccessStatusCode();

        var response2 = await _client.PutAsJsonAsync("/api/app-settings", new UpdateAppSettingsRequest { SecurityBuffer = 400m });
        response2.EnsureSuccessStatusCode();

        await using var verifyCtx = _factory.CreateDbContext();
        var row = Assert.Single(verifyCtx.AppSettings);
        Assert.Equal(1, row.Id);
        Assert.Equal(400m, row.SecurityBuffer);
    }

    [Fact]
    public async Task Update_NegativeValue_ReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("/api/app-settings", new UpdateAppSettingsRequest { SecurityBuffer = -1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var settings = await _client.GetFromJsonAsync<AppSettingsDto>("/api/app-settings");
        Assert.Equal(500m, settings!.SecurityBuffer);
    }
}
