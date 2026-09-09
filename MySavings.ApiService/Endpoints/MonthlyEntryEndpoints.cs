using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.DTOs;
using MySavings.ApiService.Models;
using MySavings.ApiService.Services;

namespace MySavings.ApiService.Endpoints;

public static class MonthlyEntryEndpoints
{
    public static void MapMonthlyEntryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monthly-entries")
            .WithTags("Monthly Entries");

        // POST create or update monthly entry
        group.MapPost("/", async (
            CreateMonthlyEntryRequest request,
            MySavingsDbContext db,
            AllocationCalculationService allocationService) =>
        {
            var monthDate = new DateTime(request.Year, request.Month, 1);

            var previousMonth = monthDate.AddMonths(-1);
            var previousMonthEntry = await db.MonthlyEntries.FirstOrDefaultAsync(e => e.Month == previousMonth);

            decimal previousPerson1Percent = 0.5m;
            decimal previousPerson2Percent = 0.5m;

            if (previousMonthEntry != null && previousMonthEntry.TotalSalary > 0)
            {
                previousPerson1Percent = previousMonthEntry.Person1Salary / previousMonthEntry.TotalSalary;
                previousPerson2Percent = previousMonthEntry.Person2Salary / previousMonthEntry.TotalSalary;
            }

            var appSettings = await db.AppSettings.FirstOrDefaultAsync();
            var securityBuffer = appSettings?.SecurityBuffer ?? MonthlyEntryCalculator.SecurityBuffer;

            var (person1Expenses, person2Expenses) = MonthlyEntryCalculator.CalculateExpenses(
                request.Person1Salary, request.Person2Salary, request.EndMonthBeforeSalary, securityBuffer);

            var entry = await db.MonthlyEntries.FirstOrDefaultAsync(e => e.Month == monthDate);
            var isCreate = entry is null;
            entry ??= new MonthlyEntry { Month = monthDate };

            MonthlyEntryCalculator.Apply(
                entry,
                request.Person1Salary,
                person1Expenses,
                request.Person2Salary,
                person2Expenses,
                previousPerson1Percent,
                previousPerson2Percent);

            if (isCreate)
                db.MonthlyEntries.Add(entry);

            await db.SaveChangesAsync();
            await allocationService.CalculateAllocationsForMonthAsync(monthDate);

            return isCreate
                ? Results.Created($"/api/monthly-entries/{entry.Id}", entry)
                : Results.Ok(entry);
        })
        .WithName("CreateOrUpdateMonthlyEntry");

        // DELETE monthly entry (cascade deletes SavingsAllocations via FK constraint)
        group.MapDelete("/{id}", async (int id, MySavingsDbContext db, AllocationCalculationService allocationService) =>
        {
            var entry = await db.MonthlyEntries.FindAsync(id);
            if (entry is null)
                return Results.NotFound();

            await allocationService.UndoTransferAccumulatorContributionsAsync(id);

            db.MonthlyEntries.Remove(entry);
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteMonthlyEntry");
    }
}
