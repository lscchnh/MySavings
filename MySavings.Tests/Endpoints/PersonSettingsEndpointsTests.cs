using System.Net.Http.Json;
using MySavings.ApiService.Endpoints;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class PersonSettingsEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public PersonSettingsEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetAll_FreshDatabase_ReturnsMigrationSeededOwners()
    {
        // The SeedDefaultPersonSettings migration inserts Louis (Id=1) and Alice (Id=2)
        // on every fresh database, so a brand-new install is never actually "empty".
        var result = await _client.GetFromJsonAsync<List<PersonSettingsDto>>("/api/person-settings");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("Louis", result[0].Name);
        Assert.Equal("Alice", result[1].Name);
    }

    [Fact]
    public async Task GetAll_ReturnsOwnersOrderedById()
    {
        await _factory.ClearOwnersAsync();
        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.Owners.AddRange(
                new Owner { Id = 8, Name = "Alice" },
                new Owner { Id = 7, Name = "Louis" });
            await seedCtx.SaveChangesAsync();
        }

        var result = await _client.GetFromJsonAsync<List<PersonSettingsDto>>("/api/person-settings");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal(7, result[0].Id);
        Assert.Equal("Louis", result[0].Name);
        Assert.Equal(8, result[1].Id);
        Assert.Equal("Alice", result[1].Name);
    }

    [Fact]
    public async Task Update_ExistingOwner_UpdatesNameAndReturnsDto()
    {
        await _factory.ClearOwnersAsync();
        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.Owners.Add(new Owner { Id = 7, Name = "Louis" });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync("/api/person-settings/7", new UpdatePersonSettingsRequest { Name = "Louis R." });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<PersonSettingsDto>();
        Assert.NotNull(dto);
        Assert.Equal(7, dto!.Id);
        Assert.Equal("Louis R.", dto.Name);

        await using var verifyCtx = _factory.CreateDbContext();
        var owner = await verifyCtx.Owners.FindAsync(7);
        Assert.Equal("Louis R.", owner!.Name);
    }

    /// <summary>
    /// Documents the exact contract that caused the frontend bug fixed earlier: when the path id
    /// doesn't match an existing owner, the endpoint creates a NEW owner with a server-assigned id
    /// rather than the requested one. Callers must use the returned Id, not the path id, to keep
    /// referring to the row afterwards.
    /// </summary>
    [Fact]
    public async Task Update_NonExistentId_CreatesNewOwnerWithServerAssignedId()
    {
        await _factory.ClearOwnersAsync();
        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.Owners.Add(new Owner { Id = 7, Name = "Louis" });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync("/api/person-settings/1", new UpdatePersonSettingsRequest { Name = "Alice" });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<PersonSettingsDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(1, dto!.Id);
        Assert.Equal("Alice", dto.Name);

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Equal(2, verifyCtx.Owners.Count());
    }
}
