using OfficeOpenXml;

namespace MySavings.Tests.Helpers;

/// <summary>
/// Generates minimal in-memory Excel workbooks for testing ExcelImportService.
/// </summary>
public static class ExcelTestHelper
{
    static ExcelTestHelper()
    {
        ExcelPackage.License.SetNonCommercialPersonal("MySavings.Tests");
    }

    /// <summary>
    /// Headers that match what ExcelImportService expects (consolidated sheet).
    /// Account columns are inserted between E/S Ratio (col 7) and LDD Alice (last account col).
    /// </summary>
    public static readonly string[] ImportHeaders =
    [
        "Date",          // col 1
        "Salaire L",     // col 2
        "Salaire A",     // col 3
        "Total Salaire", // col 4
        "Dépenses",      // col 5
        "Epargne",       // col 6
        "E/S Ratio",     // col 7
        "PEA Louis",     // col 8 — account (Person1)
        "LDD Alice",     // col 9 — account (Person2)
        "% Louis M-1",   // col 10
        "% Alice M-1",   // col 11
    ];

    /// <summary>
    /// Creates an ExcelPackage with a single worksheet containing just the header row.
    /// </summary>
    public static ExcelPackage CreateEmptyWorkbook(string[] headers)
    {
        var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Consolidé");
        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[1, i + 1].Value = headers[i];
        return package;
    }

    /// <summary>
    /// Creates an ExcelPackage with header row + one data row for the given month.
    /// Used by import tests.
    /// </summary>
    public static ExcelPackage CreateImportWorkbook(
        DateTime month,
        decimal person1Salary = 3000m,
        decimal person2Salary = 2500m,
        decimal totalExpenses = 4000m,
        decimal person1AccountValue = 500m,
        decimal person2AccountValue = 300m,
        decimal person1PercentM1 = 0.55m,
        decimal person2PercentM1 = 0.45m)
    {
        var package = CreateEmptyWorkbook(ImportHeaders);
        var sheet = package.Workbook.Worksheets[0];

        decimal totalSalary = person1Salary + person2Salary;
        decimal totalSavings = totalSalary - totalExpenses;
        decimal ratio = totalSalary > 0 ? totalSavings / totalSalary : 0;

        int row = 2;
        sheet.Cells[row, 1].Value = new DateTime(month.Year, month.Month, 1);
        sheet.Cells[row, 2].Value = person1Salary;
        sheet.Cells[row, 3].Value = person2Salary;
        sheet.Cells[row, 4].Value = totalSalary;
        sheet.Cells[row, 5].Value = totalExpenses;
        sheet.Cells[row, 6].Value = totalSavings;
        sheet.Cells[row, 7].Value = ratio;
        sheet.Cells[row, 8].Value = person1AccountValue;   // PEA Louis
        sheet.Cells[row, 9].Value = person2AccountValue;   // LDD Alice
        sheet.Cells[row, 10].Value = person1PercentM1;
        sheet.Cells[row, 11].Value = person2PercentM1;

        return package;
    }

    /// <summary>
    /// Saves an ExcelPackage to a temporary file and returns its path.
    /// The caller is responsible for deleting the file after use.
    /// </summary>
    public static string SaveToTempFile(ExcelPackage package)
    {
        var path = Path.Combine(Path.GetTempPath(), $"MySavingsTest_{Guid.NewGuid():N}.xlsx");
        package.SaveAs(new FileInfo(path));
        return path;
    }

    /// <summary>
    /// Returns the Excel package bytes as a MemoryStream (for stream-based APIs).
    /// </summary>
    public static Stream ToStream(ExcelPackage package)
    {
        var ms = new MemoryStream();
        package.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }
}
