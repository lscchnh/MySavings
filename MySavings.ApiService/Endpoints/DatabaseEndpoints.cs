using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;

namespace MySavings.ApiService.Endpoints;

public static class DatabaseEndpoints
{
    public static void MapDatabaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/database")
            .WithTags("Database");

        // GET diagnostic info for a specific month
        group.MapGet("/diagnostic/{year}/{month}", async (int year, int month, MySavingsDbContext db) =>
        {
            var targetMonth = new DateTime(year, month, 1);

            var entries = await db.MonthlyEntries
                .Include(e => e.SavingsAllocations)
                    .ThenInclude(a => a.SavingsAccount)
                .Where(e => e.Month == targetMonth)
                .ToListAsync();

            var accounts = await db.SavingsAccounts.ToListAsync();

            var result = new
            {
                Month = targetMonth.ToString("yyyy-MM"),
                Entries = entries.Select(e => new
                {
                    e.Id,
                    e.Person1Salary,
                    e.Person1Expenses,
                    e.Person1Savings,
                    e.Person1SavingsRatio,
                    e.Person2Salary,
                    e.Person2Expenses,
                    e.Person2Savings,
                    e.Person2SavingsRatio,
                    e.TotalSalary,
                    e.TotalExpenses,
                    e.TotalSavings,
                    e.TotalSavingsRatio,
                    e.PreviousMonthPerson1Percent,
                    e.PreviousMonthPerson2Percent,
                    AllocationsCount = e.SavingsAllocations.Count,
                    Allocations = e.SavingsAllocations.Select(a => new
                    {
                        a.SavingsAccount.Name,
                        a.SavingsAccount.OwnerId,
                        a.Weight,
                        a.Amount,
                        a.Percentage
                    }).ToList()
                }).ToList(),
                AllAccounts = accounts.Select(a => new
                {
                    a.Name,
                    a.OwnerId
                }).ToList()
            };

            return Results.Ok(result);
        })
        .WithName("GetDiagnosticInfo");

        // DELETE clear all data
        group.MapDelete("/clear", async (MySavingsDbContext db) =>
        {
            try
            {
                db.SavingsAllocations.RemoveRange(db.SavingsAllocations);
                db.MonthlyEntries.RemoveRange(db.MonthlyEntries);
                db.AllocationRules.RemoveRange(db.AllocationRules);
                db.TransferGroups.RemoveRange(db.TransferGroups);

                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Base de données vidée avec succès" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Erreur lors du vidage de la base : {ex.Message}");
            }
        })
        .WithName("ClearDatabase");
    }
}
