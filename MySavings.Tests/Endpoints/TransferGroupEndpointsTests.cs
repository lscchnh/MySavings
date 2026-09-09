using System.Net;
using System.Net.Http.Json;
using MySavings.ApiService.Endpoints;
using MySavings.ApiService.Models;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Endpoints;

public class TransferGroupEndpointsTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public TransferGroupEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetAll_Empty_ReturnsEmptyList()
    {
        var groups = await _client.GetFromJsonAsync<List<TransferGroupDto>>("/api/transfer-groups");

        Assert.NotNull(groups);
        Assert.Empty(groups!);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/transfer-groups", new CreateTransferGroupRequest("  ", []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_MatchesAccountsByName()
    {
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        await using (var seedCtx = _factory.CreateDbContext())
        {
            seedCtx.SavingsAccounts.AddRange(
                new SavingsAccount { Name = "PEA Louis", OwnerId = 7 },
                new SavingsAccount { Name = "LDD Alice", OwnerId = 8 });
            await seedCtx.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/transfer-groups",
            new CreateTransferGroupRequest("Virement mensuel", ["PEA Louis", "Compte inconnu"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<TransferGroupDto>();
        Assert.NotNull(dto);
        Assert.Equal("Virement mensuel", dto!.Name);
        var account = Assert.Single(dto.Accounts);
        Assert.Equal("PEA Louis", account.Name);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync("/api/transfer-groups/999",
            new UpdateTransferGroupRequest("Nouveau nom", []));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidRequest_ReplacesNameAndAccounts()
    {
        await _factory.SeedOwnersAsync((7, "Louis"), (8, "Alice"));

        int accountId;
        int groupId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var account = new SavingsAccount { Name = "PEA Louis", OwnerId = 7 };
            var other = new SavingsAccount { Name = "LDD Alice", OwnerId = 8 };
            seedCtx.SavingsAccounts.AddRange(account, other);
            await seedCtx.SaveChangesAsync();
            accountId = account.Id;

            var group = new TransferGroup { Name = "Ancien nom", SavingsAccounts = { other } };
            seedCtx.TransferGroups.Add(group);
            await seedCtx.SaveChangesAsync();
            groupId = group.Id;
        }

        var response = await _client.PutAsJsonAsync($"/api/transfer-groups/{groupId}",
            new UpdateTransferGroupRequest("Nouveau nom", ["PEA Louis"]));
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<TransferGroupDto>();
        Assert.NotNull(dto);
        Assert.Equal("Nouveau nom", dto!.Name);
        var account2 = Assert.Single(dto.Accounts);
        Assert.Equal(accountId, account2.Id);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/transfer-groups/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_RemovesGroup()
    {
        int groupId;
        await using (var seedCtx = _factory.CreateDbContext())
        {
            var group = new TransferGroup { Name = "Virement mensuel" };
            seedCtx.TransferGroups.Add(group);
            await seedCtx.SaveChangesAsync();
            groupId = group.Id;
        }

        var response = await _client.DeleteAsync($"/api/transfer-groups/{groupId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verifyCtx = _factory.CreateDbContext();
        Assert.Empty(verifyCtx.TransferGroups);
    }
}
