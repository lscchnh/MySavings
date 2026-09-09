using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;

namespace MySavings.ApiService.Endpoints;

public static class TransferGroupEndpoints
{
    public static void MapTransferGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transfer-groups")
            .WithTags("Transfer Groups");

        // GET all groups with their accounts
        group.MapGet("/", async (MySavingsDbContext db) =>
        {
            var groups = await db.TransferGroups
                .Include(g => g.SavingsAccounts)
                .ToListAsync();

            return Results.Ok(groups.Select(g => new TransferGroupDto(
                g.Id, g.Name,
                g.SavingsAccounts.Select(a => new TransferGroupAccountDto(a.Id, a.Name)).ToList()
            )));
        })
        .WithName("GetTransferGroups");

        // POST create a new group
        group.MapPost("/", async (CreateTransferGroupRequest request, MySavingsDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Le libellé est obligatoire." });

            var accountNames = request.AccountNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();

            var accounts = await db.SavingsAccounts
                .Where(a => accountNames.Contains(a.Name))
                .ToListAsync();

            var transferGroup = new TransferGroup
            {
                Name = request.Name.Trim(),
                SavingsAccounts = accounts
            };

            db.TransferGroups.Add(transferGroup);
            await db.SaveChangesAsync();

            return Results.Created($"/api/transfer-groups/{transferGroup.Id}",
                new TransferGroupDto(transferGroup.Id, transferGroup.Name,
                    transferGroup.SavingsAccounts.Select(a => new TransferGroupAccountDto(a.Id, a.Name)).ToList()));
        })
        .WithName("CreateTransferGroup");

        // PUT update a group (name and replace all accounts)
        group.MapPut("/{id:int}", async (int id, UpdateTransferGroupRequest request, MySavingsDbContext db) =>
        {
            var transferGroup = await db.TransferGroups
                .Include(g => g.SavingsAccounts)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (transferGroup is null)
                return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Le libellé est obligatoire." });

            transferGroup.Name = request.Name.Trim();

            var accountNames = request.AccountNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();

            var accounts = await db.SavingsAccounts
                .Where(a => accountNames.Contains(a.Name))
                .ToListAsync();

            transferGroup.SavingsAccounts.Clear();
            foreach (var account in accounts)
                transferGroup.SavingsAccounts.Add(account);

            await db.SaveChangesAsync();

            return Results.Ok(new TransferGroupDto(transferGroup.Id, transferGroup.Name,
                transferGroup.SavingsAccounts.Select(a => new TransferGroupAccountDto(a.Id, a.Name)).ToList()));
        })
        .WithName("UpdateTransferGroup");

        // DELETE a group
        group.MapDelete("/{id:int}", async (int id, MySavingsDbContext db) =>
        {
            var transferGroup = await db.TransferGroups.FindAsync(id);
            if (transferGroup is null)
                return Results.NotFound();

            db.TransferGroups.Remove(transferGroup);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteTransferGroup");
    }
}

public record TransferGroupAccountDto(int Id, string Name);
public record TransferGroupDto(int Id, string Name, List<TransferGroupAccountDto> Accounts);
public record CreateTransferGroupRequest(string Name, List<string> AccountNames);
public record UpdateTransferGroupRequest(string Name, List<string> AccountNames);
