using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;

namespace MySavings.ApiService.Endpoints;

public static class PersonSettingsEndpoints
{
    public static void MapPersonSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/person-settings")
            .WithTags("Person Settings");

        // GET all owners
        group.MapGet("/", async (MySavingsDbContext db) =>
        {
            var owners = await db.Owners
                .OrderBy(o => o.Id)
                .Select(o => new PersonSettingsDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    LastModified = o.LastModified,
                    AllocationSharePercent = o.AllocationSharePercent
                })
                .ToListAsync();

            return Results.Ok(owners);
        })
        .WithName("GetAllPersonSettings");

        // PUT update owner by id
        group.MapPut("/{id:int}", async (int id, UpdatePersonSettingsRequest request, MySavingsDbContext db) =>
        {
            var owner = await db.Owners.FindAsync(id);

            if (owner is null)
            {
                owner = new Owner
                {
                    Name = request.Name,
                    LastModified = DateTime.UtcNow
                };
                db.Owners.Add(owner);
            }
            else
            {
                owner.Name = request.Name;
                owner.LastModified = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new PersonSettingsDto
            {
                Id = owner.Id,
                Name = owner.Name,
                LastModified = owner.LastModified,
                AllocationSharePercent = owner.AllocationSharePercent
            });
        })
        .WithName("UpdatePersonSettings");
    }
}

public record PersonSettingsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public decimal AllocationSharePercent { get; set; }
}

public record UpdatePersonSettingsRequest
{
    public string Name { get; set; } = string.Empty;
}
