using MySavings.ApiService.Models;
using MySavings.ApiService.Services;

namespace MySavings.Tests.Services;

public class MonthlyEntryCalculatorTests
{
    // ──────────────────────────────────────────────
    // CalculateExpenses
    // ──────────────────────────────────────────────

    [Fact]
    public void CalculateExpenses_EqualSalaries_SplitsExpensesEvenly()
    {
        // Salaires égaux → chacun paie exactement la moitié des dépenses totales
        // TotalExpenses = (3000+3000) - 2000 + 500 = 4500
        // PersonExpenses = 4500 / 2 = 2250
        var (p1, p2) = MonthlyEntryCalculator.CalculateExpenses(3000m, 3000m, 2000m);

        Assert.Equal(2250m, p1);
        Assert.Equal(2250m, p2);
    }

    [Fact]
    public void CalculateExpenses_UnequalSalaries_SplitsProportionally()
    {
        // Person1 = 4000, Person2 = 1000 → Person1 paie 80 % des dépenses
        // TotalExpenses = 5000 - 1000 + 500 = 4500
        // Person1 = 0.8 × 4500 = 3600 ; Person2 = 0.2 × 4500 = 900
        var (p1, p2) = MonthlyEntryCalculator.CalculateExpenses(4000m, 1000m, 1000m);

        Assert.Equal(3600m, p1);
        Assert.Equal(900m, p2);
    }

    [Fact]
    public void CalculateExpenses_ZeroTotalSalary_ReturnsZeros()
    {
        var (p1, p2) = MonthlyEntryCalculator.CalculateExpenses(0m, 0m, 1000m);

        Assert.Equal(0m, p1);
        Assert.Equal(0m, p2);
    }

    [Fact]
    public void CalculateExpenses_SecurityBufferIncludedInExpenses()
    {
        // SecurityBuffer = 500 doit bien être inclus
        // TotalExpenses = 3000 - 1000 + 500 = 2500 (tout pour Person1 seul)
        var (p1, _) = MonthlyEntryCalculator.CalculateExpenses(3000m, 0m, 1000m);

        // TotalSalary = 3000 → Person1 paie 100 % → 2500
        Assert.Equal(2500m, p1);
    }

    [Fact]
    public void CalculateExpenses_SecurityBuffer_HasCorrectConstantValue()
    {
        Assert.Equal(500m, MonthlyEntryCalculator.SecurityBuffer);
    }

    // ──────────────────────────────────────────────
    // Apply
    // ──────────────────────────────────────────────

    [Fact]
    public void Apply_SetsAllEntryFields()
    {
        var entry = new MonthlyEntry();

        MonthlyEntryCalculator.Apply(entry,
            person1Salary: 3000m, person1Expenses: 2000m,
            person2Salary: 2000m, person2Expenses: 1500m,
            previousPerson1Percent: 0.6m, previousPerson2Percent: 0.4m);

        // Salaires
        Assert.Equal(3000m, entry.Person1Salary);
        Assert.Equal(2000m, entry.Person2Salary);
        Assert.Equal(5000m, entry.TotalSalary);

        // Épargnes
        Assert.Equal(1000m, entry.Person1Savings);
        Assert.Equal(500m, entry.Person2Savings);
        Assert.Equal(1500m, entry.TotalSavings);

        // Dépenses
        Assert.Equal(2000m, entry.Person1Expenses);
        Assert.Equal(1500m, entry.Person2Expenses);
        Assert.Equal(3500m, entry.TotalExpenses);

        // Ratios
        Assert.Equal(1000m / 3000m, entry.Person1SavingsRatio);
        Assert.Equal(500m / 2000m, entry.Person2SavingsRatio);
        Assert.Equal(1500m / 5000m, entry.TotalSavingsRatio);

        // Pourcentages M-1
        Assert.Equal(0.6m, entry.PreviousMonthPerson1Percent);
        Assert.Equal(0.4m, entry.PreviousMonthPerson2Percent);
    }

    [Fact]
    public void Apply_ZeroSalary_RatioIsZero()
    {
        var entry = new MonthlyEntry();

        MonthlyEntryCalculator.Apply(entry,
            person1Salary: 0m, person1Expenses: 0m,
            person2Salary: 2000m, person2Expenses: 1500m,
            previousPerson1Percent: 0m, previousPerson2Percent: 1m);

        Assert.Equal(0m, entry.Person1SavingsRatio);
        Assert.Equal(500m / 2000m, entry.Person2SavingsRatio);
        Assert.Equal(500m / 2000m, entry.TotalSavingsRatio);
    }

    [Fact]
    public void Apply_BothZeroSalaries_AllRatiosAreZero()
    {
        var entry = new MonthlyEntry();

        MonthlyEntryCalculator.Apply(entry,
            0m, 0m, 0m, 0m, 0.5m, 0.5m);

        Assert.Equal(0m, entry.Person1SavingsRatio);
        Assert.Equal(0m, entry.Person2SavingsRatio);
        Assert.Equal(0m, entry.TotalSavingsRatio);
    }

    [Fact]
    public void CalculateExpenses_ThenApply_RoundTrip_ProducesConsistentSavings()
    {
        // Scénario complet : les dépenses calculées conduisent à une épargne positive
        decimal p1Salary = 3500m;
        decimal p2Salary = 2500m;
        decimal endMonth = 2000m;

        var (p1Exp, p2Exp) = MonthlyEntryCalculator.CalculateExpenses(p1Salary, p2Salary, endMonth);

        var entry = new MonthlyEntry();
        MonthlyEntryCalculator.Apply(entry, p1Salary, p1Exp, p2Salary, p2Exp, 0.58m, 0.42m);

        // TotalExpenses = (3500+2500) - 2000 + 500 = 4500
        Assert.Equal(4500m, entry.TotalExpenses);

        // TotalSavings = TotalSalary - TotalExpenses = 6000 - 4500 = 1500
        Assert.Equal(1500m, entry.TotalSavings);
    }
}
