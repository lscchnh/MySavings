using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;

namespace MySavings.ApiService.Endpoints;

public static class SavingsAllocationEndpoints
{
    public static void MapSavingsAllocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/savings-allocations")
            .WithTags("Savings Allocations");

        // PATCH toggle IsTransferred for a savings allocation
        group.MapPatch("/{id:int}/transferred", async (int id, SetTransferredRequest request, MySavingsDbContext db) =>
        {
            var allocation = await db.SavingsAllocations.FirstOrDefaultAsync(a => a.Id == id);
            if (allocation is null)
                return Results.NotFound();

            allocation.IsTransferred = request.IsTransferred;
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("SetAllocationTransferred");
    }
}

public record SetTransferredRequest(bool IsTransferred);
