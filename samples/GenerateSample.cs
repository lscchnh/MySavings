#:package EPPlus@8.6.3

// Génère samples/mysavings-sample.xlsx : 24 mois de données FICTIVES importables via la page Import.
// Usage (depuis la racine du dépôt) : dotnet run samples/GenerateSample.cs
using OfficeOpenXml;

ExcelPackage.License.SetNonCommercialPersonal("MySavings");

// Propriétaires créés par défaut par les migrations ; le dernier mot du nom de compte désigne le propriétaire.
const string person1 = "Louis";
const string person2 = "Alice";

(string Name, decimal Weight)[] accounts =
[
    ($"PEA {person1}", 0.20m),
    ($"Assurance-vie {person1}", 0.15m),
    ($"Livret A {person1}", 0.15m),
    ($"LDDS {person2}", 0.20m),
    ($"PEA {person2}", 0.20m),
    ($"Livret A {person2}", 0.10m),
];

var rng = new Random(42);
var firstMonth = new DateTime(2024, 9, 1);
const int months = 24;

var headers = new List<string> { "Date", $"Salaire {person1}", $"Salaire {person2}", "Total Salaire", "Dépenses", "Epargne", "E/S Ratio" };
var accountStartCol = headers.Count + 1;
var percentCol1 = accountStartCol + accounts.Length * 2;
var percentCol2 = percentCol1 + 1;

using var package = new ExcelPackage();
var sheet = package.Workbook.Worksheets.Add("Consolidation");

for (var i = 0; i < headers.Count; i++)
    sheet.Cells[1, i + 1].Value = headers[i];

// Chaque compte occupe deux colonnes fusionnées en ligne 1 : poids d'allocation, puis montant alloué.
for (var a = 0; a < accounts.Length; a++)
{
    var col = accountStartCol + a * 2;
    sheet.Cells[1, col, 1, col + 1].Merge = true;
    sheet.Cells[1, col].Value = accounts[a].Name;
}

sheet.Cells[1, percentCol1].Value = $"% {person1} M-1";
sheet.Cells[1, percentCol2].Value = $"% {person2} M-1";
sheet.Cells[1, 1, 1, percentCol2].Style.Font.Bold = true;

var salary1 = 3200m;
var salary2 = 2700m;
decimal? previousShare1 = null;

for (var m = 0; m < months; m++)
{
    var row = m + 2;
    var month = firstMonth.AddMonths(m);

    if (month.Month == 1)
    {
        salary1 = Math.Round(salary1 * 1.03m, 0);
        salary2 = Math.Round(salary2 * 1.02m, 0);
    }

    var bonus1 = month.Month == 12 ? 1500m : 0m;
    var total = salary1 + salary2 + bonus1;
    var expenses = Math.Round(4300m + rng.Next(-400, 700) + (month.Month is 8 or 12 ? 500 : 0), 0);
    var savings = total - expenses;

    var share1 = previousShare1 ?? 0.5m;
    var share2 = 1m - share1;

    sheet.Cells[row, 1].Value = month;
    sheet.Cells[row, 1].Style.Numberformat.Format = "mm/yyyy";
    sheet.Cells[row, 2].Value = salary1 + bonus1;
    sheet.Cells[row, 3].Value = salary2;
    sheet.Cells[row, 4].Value = total;
    sheet.Cells[row, 5].Value = expenses;
    sheet.Cells[row, 6].Value = savings;
    sheet.Cells[row, 7].Value = Math.Round(savings / total, 4);
    sheet.Cells[row, 7].Style.Numberformat.Format = "0.00%";

    for (var a = 0; a < accounts.Length; a++)
    {
        var col = accountStartCol + a * 2;
        var ownerShare = accounts[a].Name.EndsWith(person1) ? share1 : share2;
        sheet.Cells[row, col].Value = accounts[a].Weight;
        sheet.Cells[row, col].Style.Numberformat.Format = "0.00%";
        sheet.Cells[row, col + 1].Value = savings > 0 ? Math.Round(accounts[a].Weight * (ownerShare / 0.5m) * savings, 2) : 0m;
    }

    sheet.Cells[row, percentCol1].Value = share1;
    sheet.Cells[row, percentCol2].Value = share2;
    sheet.Cells[row, percentCol1, row, percentCol2].Style.Numberformat.Format = "0.00%";

    // Le mois suivant utilise la contribution de ce mois (M-1).
    previousShare1 = Math.Round((salary1 + bonus1) / total, 4);
}

sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

var output = Path.Combine("samples", "mysavings-sample.xlsx");
package.SaveAs(new FileInfo(output));
Console.WriteLine($"Écrit : {output}");
