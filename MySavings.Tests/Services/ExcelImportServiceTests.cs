using Microsoft.Extensions.Logging.Abstractions;
using MySavings.ApiService.Services;
using MySavings.Tests.Helpers;

namespace MySavings.Tests.Services;

public class ExcelImportServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private ExcelImportService BuildService()
    {
        var ctx = _factory.CreateContext();
        return new ExcelImportService(ctx, NullLogger<ExcelImportService>.Instance);
    }

    // ─────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromExcelAsync_InvalidStream_ReturnsFailure()
    {
        // Un stream qui n'est pas un fichier xlsx valide → service doit retourner échec
        using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03, 0xFF]);

        var svc = BuildService();
        var result = await svc.ImportFromExcelAsync(stream);

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task ImportFromExcelAsync_ValidSingleRow_CreatesMonthlyEntry()
    {
        var month = new DateTime(2024, 1, 1);
        using var pkg = ExcelTestHelper.CreateImportWorkbook(month,
            person1Salary: 3000m,
            person2Salary: 2500m,
            totalExpenses: 4000m);
        using var stream = ExcelTestHelper.ToStream(pkg);

        var svc = BuildService();
        var result = await svc.ImportFromExcelAsync(stream);

        Assert.True(result.Success);

        await using var ctx = _factory.CreateContext();
        var entry = ctx.MonthlyEntries.Single();
        Assert.Equal(month, entry.Month);
        Assert.Equal(3000m, entry.Person1Salary);
        Assert.Equal(2500m, entry.Person2Salary);
        Assert.Equal(5500m, entry.TotalSalary);
    }

    [Fact]
    public async Task ImportFromExcelAsync_TwoRows_CreatesTwoMonthlyEntries()
    {
        using var pkg = ExcelTestHelper.CreateEmptyWorkbook(ExcelTestHelper.ImportHeaders);
        var sheet = pkg.Workbook.Worksheets[0];

        // Ligne 2
        sheet.Cells[2, 1].Value = new DateTime(2024, 1, 1);
        sheet.Cells[2, 2].Value = 3000m;
        sheet.Cells[2, 3].Value = 2500m;
        sheet.Cells[2, 4].Value = 5500m;
        sheet.Cells[2, 5].Value = 4000m;
        sheet.Cells[2, 6].Value = 1500m;
        sheet.Cells[2, 7].Value = 1500m / 5500m;
        sheet.Cells[2, 8].Value = 400m;  // PEA Louis
        sheet.Cells[2, 9].Value = 300m;  // LDD Alice
        sheet.Cells[2, 10].Value = 0.55m;
        sheet.Cells[2, 11].Value = 0.45m;

        // Ligne 3
        sheet.Cells[3, 1].Value = new DateTime(2024, 2, 1);
        sheet.Cells[3, 2].Value = 3100m;
        sheet.Cells[3, 3].Value = 2600m;
        sheet.Cells[3, 4].Value = 5700m;
        sheet.Cells[3, 5].Value = 4100m;
        sheet.Cells[3, 6].Value = 1600m;
        sheet.Cells[3, 7].Value = 1600m / 5700m;
        sheet.Cells[3, 8].Value = 420m;
        sheet.Cells[3, 9].Value = 320m;
        sheet.Cells[3, 10].Value = 0.54m;
        sheet.Cells[3, 11].Value = 0.46m;

        using var stream = ExcelTestHelper.ToStream(pkg);
        var svc = BuildService();
        await svc.ImportFromExcelAsync(stream);

        await using var ctx = _factory.CreateContext();
        Assert.Equal(2, ctx.MonthlyEntries.Count());
    }

    [Fact]
    public async Task ImportFromExcelAsync_ExistingEntry_UpdatesInsteadOfCreating()
    {
        var month = new DateTime(2024, 3, 1);

        // Premier import
        using (var pkg1 = ExcelTestHelper.CreateImportWorkbook(month, person1Salary: 3000m, person2Salary: 2500m))
        {
            using var s1 = ExcelTestHelper.ToStream(pkg1);
            var svc1 = BuildService();
            await svc1.ImportFromExcelAsync(s1);
        }

        // Deuxième import avec salaires différents
        using (var pkg2 = ExcelTestHelper.CreateImportWorkbook(month, person1Salary: 3200m, person2Salary: 2700m))
        {
            using var s2 = ExcelTestHelper.ToStream(pkg2);
            var svc2 = BuildService();
            await svc2.ImportFromExcelAsync(s2);
        }

        await using var ctx = _factory.CreateContext();
        // Toujours une seule entrée pour ce mois
        Assert.Equal(1, ctx.MonthlyEntries.Count());
        var entry = ctx.MonthlyEntries.Single();
        Assert.Equal(3200m, entry.Person1Salary);
        Assert.Equal(2700m, entry.Person2Salary);
    }

    [Fact]
    public async Task ImportFromExcelAsync_ReturnsSuccessTrue_OnValidFile()
    {
        var month = new DateTime(2024, 5, 1);
        using var pkg = ExcelTestHelper.CreateImportWorkbook(month);
        using var stream = ExcelTestHelper.ToStream(pkg);

        var svc = BuildService();
        var result = await svc.ImportFromExcelAsync(stream);

        Assert.True(result.Success);
        Assert.Contains("Import successful", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
