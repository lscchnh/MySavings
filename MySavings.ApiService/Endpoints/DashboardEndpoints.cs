using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.DTOs;

namespace MySavings.ApiService.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard");

        // GET dashboard summary
        group.MapGet("/summary", async (MySavingsDbContext db) =>
        {
            var lastMonthWithData = await db.MonthlyEntries
                .OrderByDescending(e => e.Month)
                .Select(e => e.Month)
                .FirstOrDefaultAsync();

            var currentMonth = lastMonthWithData != DateTime.MinValue
                ? lastMonthWithData
                : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);

            var months = await db.MonthlyEntries
                .Select(e => new MonthlyTrendDto
                {
                    Month = e.Month,
                    Salary = e.TotalSalary,
                    Expenses = e.TotalExpenses,
                    Savings = e.TotalSavings,
                    SavingsRatio = e.TotalSavingsRatio
                })
                .OrderBy(t => t.Month)
                .ToListAsync();

            var currentMonthEntry = await db.MonthlyEntries
                .FirstOrDefaultAsync(e => e.Month == currentMonth);

            var currentSalary = currentMonthEntry?.TotalSalary ?? 0;
            var currentExpenses = currentMonthEntry?.TotalExpenses ?? 0;
            var currentSavings = currentMonthEntry?.TotalSavings ?? 0;

            var summary = new DashboardSummaryDto
            {
                CurrentMonth = currentMonth,
                TotalSalary = currentSalary,
                TotalExpenses = currentExpenses,
                TotalSavings = currentSavings,
                SavingsRatio = currentSalary > 0 ? currentSavings / currentSalary : 0,
                Months = months
            };

            return Results.Ok(summary);
        })
        .WithName("GetDashboardSummary");

        // GET all consolidations (history)
        group.MapGet("/history", async (MySavingsDbContext db) =>
        {
            var entries = await db.MonthlyEntries
                .Include(e => e.SavingsAllocations)
                    .ThenInclude(a => a.SavingsAccount)
                .OrderByDescending(e => e.Month)
                .ToListAsync();

            var consolidations = entries.Select(entry =>
            {
                var allocations = entry.SavingsAllocations
                    .Select(a => new ConsolidatedAllocationDto
                    {
                        Id = a.Id,
                        AccountName = a.SavingsAccount.Name,
                        OwnerId = a.SavingsAccount.OwnerId,
                        Amount = a.Amount,
                        Weight = a.Weight,
                        Percentage = a.Percentage,
                        IsTransferred = a.IsTransferred,
                        TransferableAmount = a.TransferableAmount
                    })
                    .OrderBy(a => a.OwnerId)
                    .ThenBy(a => a.AccountName)
                    .ToList();

                return new ConsolidatedViewDto
                {
                    Id = entry.Id,
                    Month = entry.Month,
                    Person1Salary = entry.Person1Salary,
                    Person1Expenses = entry.Person1Expenses,
                    Person1Savings = entry.Person1Savings,
                    Person1SavingsRatio = entry.Person1SavingsRatio,
                    Person2Salary = entry.Person2Salary,
                    Person2Expenses = entry.Person2Expenses,
                    Person2Savings = entry.Person2Savings,
                    Person2SavingsRatio = entry.Person2SavingsRatio,
                    TotalSalary = entry.TotalSalary,
                    TotalExpenses = entry.TotalExpenses,
                    TotalSavings = entry.TotalSavings,
                    TotalSavingsRatio = entry.TotalSavingsRatio,
                    Person1ContributionPercent = entry.PreviousMonthPerson1Percent,
                    Person2ContributionPercent = entry.PreviousMonthPerson2Percent,
                    Allocations = allocations
                };
            }).ToList();

            return Results.Ok(consolidations);
        })
        .WithName("GetDashboardHistory");
    }
}
