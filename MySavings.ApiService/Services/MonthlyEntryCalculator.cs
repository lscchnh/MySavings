using MySavings.ApiService.Models;

namespace MySavings.ApiService.Services;

/// <summary>
/// Pure static calculation helpers for monthly entries.
/// Separated from the endpoint to allow unit testing without HTTP infrastructure.
/// </summary>
public static class MonthlyEntryCalculator
{
    /// <summary>
    /// Default security buffer kept on the current account (not counted as savings),
    /// used when no configured value is available. Configurable via AppSettings.SecurityBuffer.
    /// </summary>
    public const decimal SecurityBuffer = 500m;

    /// <summary>
    /// Calculates each person's expense share from the entry inputs.
    /// Formula: TotalExpenses = TotalSalary - EndMonthBeforeSalary + SecurityBuffer
    ///          PersonExpenses = (PersonSalary / TotalSalary) × TotalExpenses
    /// </summary>
    public static (decimal person1Expenses, decimal person2Expenses) CalculateExpenses(
        decimal person1Salary,
        decimal person2Salary,
        decimal endMonthBeforeSalary,
        decimal securityBuffer = SecurityBuffer)
    {
        var totalSalary = person1Salary + person2Salary;
        var totalExpenses = totalSalary - endMonthBeforeSalary + securityBuffer;

        if (totalSalary <= 0)
            return (0, 0);

        return (
            (person1Salary / totalSalary) * totalExpenses,
            (person2Salary / totalSalary) * totalExpenses
        );
    }

    /// <summary>
    /// Applies all computed financial values to an existing or new MonthlyEntry.
    /// </summary>
    public static void Apply(
        MonthlyEntry entry,
        decimal person1Salary,
        decimal person1Expenses,
        decimal person2Salary,
        decimal person2Expenses,
        decimal previousPerson1Percent,
        decimal previousPerson2Percent)
    {
        var person1Savings = person1Salary - person1Expenses;
        var person1Ratio = person1Salary > 0 ? person1Savings / person1Salary : 0;

        var person2Savings = person2Salary - person2Expenses;
        var person2Ratio = person2Salary > 0 ? person2Savings / person2Salary : 0;

        var totalSalary = person1Salary + person2Salary;
        var totalExpenses = person1Expenses + person2Expenses;
        var totalSavings = person1Savings + person2Savings;
        var totalRatio = totalSalary > 0 ? totalSavings / totalSalary : 0;

        entry.Person1Salary = person1Salary;
        entry.Person1Expenses = person1Expenses;
        entry.Person1Savings = person1Savings;
        entry.Person1SavingsRatio = person1Ratio;
        entry.Person2Salary = person2Salary;
        entry.Person2Expenses = person2Expenses;
        entry.Person2Savings = person2Savings;
        entry.Person2SavingsRatio = person2Ratio;
        entry.TotalSalary = totalSalary;
        entry.TotalExpenses = totalExpenses;
        entry.TotalSavings = totalSavings;
        entry.TotalSavingsRatio = totalRatio;
        entry.PreviousMonthPerson1Percent = previousPerson1Percent;
        entry.PreviousMonthPerson2Percent = previousPerson2Percent;
    }
}
