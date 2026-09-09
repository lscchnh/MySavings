using Microsoft.EntityFrameworkCore;
using MySavings.ApiService.Data;
using MySavings.ApiService.Models;
using OfficeOpenXml;

namespace MySavings.ApiService.Services;

public class ExcelImportService(
    MySavingsDbContext db,
    ILogger<ExcelImportService> logger)
{
    /// <summary>
    /// Imports data from an Excel file from the first worksheet.
    /// </summary>
    public async Task<ImportResult> ImportFromExcelAsync(Stream excelStream)
    {
        var result = new ImportResult();

        try
        {
            using var package = new ExcelPackage(excelStream);

            var firstSheet = package.Workbook.Worksheets.FirstOrDefault();

            if (firstSheet == null)
            {
                result.Success = false;
                result.Message = "No worksheet found in the Excel file";
                return result;
            }

            var (person1Count, person2Count, person1Name, person2Name, rulesImported) = await ImportConsolidatedSheetAsync(firstSheet);
            result.Person1Entries = person1Count;
            result.Person2Entries = person2Count;
            result.AllocationRulesImported = rulesImported;

            result.Success = true;

            var p1DisplayName = !string.IsNullOrEmpty(person1Name) ? person1Name : "Person 1";
            var p2DisplayName = !string.IsNullOrEmpty(person2Name) ? person2Name : "Person 2";

            result.Message = $"Import successful: {result.Person1Entries} entries for {p1DisplayName}, {result.Person2Entries} entries for {p2DisplayName}";

            if (result.AllocationRulesImported > 0)
                result.Message += $", {result.AllocationRulesImported} allocation rules imported";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error during import: {ex.Message}";
            logger.LogError(ex, "Error during Excel import");
        }

        return result;
    }

    /// <summary>
    /// Exports a consolidated workbook built from all DB entries.
    /// </summary>
    public async Task<(bool IsSuccess, byte[]? Content, string? ErrorMessage)> ExportWorkbookAsync()
    {
        try
        {
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Consolidation");

            var entries = await db.MonthlyEntries
                .Include(e => e.SavingsAllocations)
                    .ThenInclude(a => a.SavingsAccount)
                .OrderBy(e => e.Month)
                .ToListAsync();

            var owners = await db.Owners.OrderBy(o => o.Id).ToListAsync();
            var person1Name = owners.ElementAtOrDefault(0)?.Name ?? "Louis";
            var person2Name = owners.ElementAtOrDefault(1)?.Name ?? "Alice";

            var accounts = await db.SavingsAccounts
                .OrderBy(a => a.OwnerId)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var allocationRuleWeightsByAccountId = await db.AllocationRules
                .ToDictionaryAsync(r => r.SavingsAccountId, r => r.Weight);

            var headers = new List<string>
            {
                "Date",
                $"Salaire {person1Name}",
                $"Salaire {person2Name}",
                $"% {person2Name} M-1",
                $"% {person1Name} M-1",
                "Total Salaire",
                "Dépenses",
                "Epargne",
                "E/S Ratio"
            };

            foreach (var account in accounts)
            {
                headers.Add($"% {account.Name}");
                headers.Add(account.Name);
            }

            for (int col = 1; col <= headers.Count; col++)
            {
                sheet.Cells[1, col].Value = headers[col - 1];
                sheet.Cells[1, col].Style.Font.Bold = true;
            }

            var person2PercentCol = 4;
            var person1PercentCol = 5;
            var accountStartCol = 10;

            for (int i = 0; i < entries.Count; i++)
            {
                var row = i + 2;
                var entry = entries[i];

                sheet.Cells[row, 1].Value = new DateTime(entry.Month.Year, entry.Month.Month, 1);
                sheet.Cells[row, 1].Style.Numberformat.Format = "mm/yyyy";

                sheet.Cells[row, 2].Value = (double)entry.Person1Salary;
                sheet.Cells[row, 3].Value = (double)entry.Person2Salary;
                sheet.Cells[row, person2PercentCol].Value = (double)entry.PreviousMonthPerson2Percent;
                sheet.Cells[row, person1PercentCol].Value = (double)entry.PreviousMonthPerson1Percent;
                sheet.Cells[row, 6].Value = (double)entry.TotalSalary;
                sheet.Cells[row, 7].Value = (double)entry.TotalExpenses;
                sheet.Cells[row, 8].Value = (double)entry.TotalSavings;
                sheet.Cells[row, 9].Value = (double)entry.TotalSavingsRatio;

                var allocationsByAccountId = entry.SavingsAllocations
                    .GroupBy(a => a.SavingsAccountId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

                for (int accountIndex = 0; accountIndex < accounts.Count; accountIndex++)
                {
                    var account = accounts[accountIndex];
                    var percentCol = accountStartCol + (accountIndex * 2);
                    var amountCol = percentCol + 1;

                    allocationsByAccountId.TryGetValue(account.Id, out var amount);
                    allocationRuleWeightsByAccountId.TryGetValue(account.Id, out var weight);

                    sheet.Cells[row, percentCol].Value = (double)weight;
                    sheet.Cells[row, amountCol].Value = (double)amount;
                    sheet.Cells[row, percentCol].Style.Numberformat.Format = "0.00%";
                }

                sheet.Cells[row, 9].Style.Numberformat.Format = "0.00%";
                sheet.Cells[row, person1PercentCol].Style.Numberformat.Format = "0.00%";
                sheet.Cells[row, person2PercentCol].Style.Numberformat.Format = "0.00%";
            }

            if (sheet.Dimension != null)
                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return (true, bytes, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while exporting Excel workbook");
            return (false, null, $"Error while exporting Excel: {ex.Message}");
        }
    }

    private async Task<(int person1Count, int person2Count, string person1Name, string person2Name, int rulesImported)> ImportConsolidatedSheetAsync(ExcelWorksheet sheet)
    {
        int person1Count = 0;
        int person2Count = 0;

        int startRow = 2;

        // The owner with the lowest Id is Person1, the next one is Person2 (owner ids aren't
        // guaranteed to start at 1/2 in practice, e.g. after data has been re-seeded).
        var owners = await db.Owners.OrderBy(o => o.Id).ToListAsync();
        var person1Owner = owners.ElementAtOrDefault(0);
        var person2Owner = owners.ElementAtOrDefault(1);
        var dbPerson1Name = person1Owner?.Name ?? string.Empty;
        var dbPerson2Name = person2Owner?.Name ?? string.Empty;

        int dateCol = FindColumnIndex(sheet, "Date");
        int p1SalaryCol = FindColumnIndex(sheet, ["Salaire L", "Person1", "Personne 1", "P1", .. (!string.IsNullOrEmpty(dbPerson1Name) ? (string[])[dbPerson1Name, $"Salaire {dbPerson1Name[0]}"] : [])]);
        int p2SalaryCol = FindColumnIndex(sheet, ["Salaire A", "Person2", "Personne 2", "P2", .. (!string.IsNullOrEmpty(dbPerson2Name) ? (string[])[dbPerson2Name, $"Salaire {dbPerson2Name[0]}"] : [])]);
        int totalSalaryCol = FindColumnIndex(sheet, "Total Salaire", "Total Salary");
        int expensesCol = FindColumnIndex(sheet, "Dépenses", "Depenses", "Expenses");
        int totalSavingsCol = FindColumnIndex(sheet, "Epargne", "Épargne", "Savings");
        int ratioCol = FindColumnIndex(sheet, "E/S Ratio", "Ratio");
        int person1PercentM1Col = FindColumnIndex(sheet, ["% L M-1", .. (!string.IsNullOrEmpty(dbPerson1Name) ? (string[])[$"% {dbPerson1Name} M-1", $"%{dbPerson1Name} M-1", $"% {dbPerson1Name} (M-1)", $"%{dbPerson1Name}(M-1)", $"{dbPerson1Name} M-1", $"{dbPerson1Name}(M-1)"] : [])]);
        int person2PercentM1Col = FindColumnIndex(sheet, ["% A M-1", .. (!string.IsNullOrEmpty(dbPerson2Name) ? (string[])[$"% {dbPerson2Name} M-1", $"%{dbPerson2Name} M-1", $"% {dbPerson2Name} (M-1)", $"%{dbPerson2Name}(M-1)", $"{dbPerson2Name} M-1", $"{dbPerson2Name}(M-1)"] : [])]);

        var accountColumns = await DetectAccountColumnsAsync(sheet, dbPerson1Name, person1Owner?.Id, dbPerson2Name, person2Owner?.Id);

        if (dateCol == -1 || p1SalaryCol == -1 || p2SalaryCol == -1 || expensesCol == -1)
        {
            logger.LogWarning("Unable to find all required columns. Found - Month: {DateCol}, P1Salary: {P1Col}, P2Salary: {P2Col}, Expenses: {ExpCol}", dateCol, p1SalaryCol, p2SalaryCol, expensesCol);

            if (dateCol == -1) dateCol = 1;
            if (p1SalaryCol == -1) p1SalaryCol = 2;
            if (p2SalaryCol == -1) p2SalaryCol = 3;
            if (expensesCol == -1) expensesCol = 5;
        }

        logger.LogInformation("Detected columns - Month: {DateCol}, P1Salary: {P1Col}, P2Salary: {P2Col}, Expenses: {ExpCol}, Person1%M-1: {P1PctCol}, Person2%M-1: {P2PctCol}, Accounts: {AccountCount}", dateCol, p1SalaryCol, p2SalaryCol, expensesCol, person1PercentM1Col, person2PercentM1Col, accountColumns.Count);

        int rulesImported = 0;
        if (accountColumns.Count != 0 && sheet.Dimension.End.Row >= startRow)
            rulesImported = await ImportAllocationRulesFromRow(sheet, sheet.Dimension.End.Row, accountColumns);

        var allAccounts = await db.SavingsAccounts.ToDictionaryAsync(a => a.Name, a => a, StringComparer.OrdinalIgnoreCase);
        var allRules = await db.AllocationRules.ToDictionaryAsync(r => r.SavingsAccountId, r => r);

        for (int row = startRow; row <= sheet.Dimension.End.Row; row++)
        {
            try
            {
                var dateValue = sheet.Cells[row, dateCol].Value;
                if (dateValue == null)
                    continue;

                DateTime month;

                if (dateValue is DateTime dateTime)
                {
                    month = new DateTime(dateTime.Year, dateTime.Month, 1);
                    logger.LogInformation("Row {Row}: Date = {Date:dd/MM/yyyy} -> Normalized to {Month:yyyy-MM-dd}", row, dateTime, month);
                }
                else if (dateValue is double oleDate)
                {
                    try
                    {
                        dateTime = DateTime.FromOADate(oleDate);
                        month = new DateTime(dateTime.Year, dateTime.Month, 1);
                        logger.LogInformation("Row {Row}: OLE date {OleDate} = {Date:dd/MM/yyyy} -> Normalized to {Month:yyyy-MM-dd}", row, oleDate, dateTime, month);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("Invalid OLE date at row {Row}: {OleDate} - {Error}", row, oleDate, ex.Message);
                        continue;
                    }
                }
                else if (DateTime.TryParse(dateValue.ToString(), out dateTime))
                {
                    month = new DateTime(dateTime.Year, dateTime.Month, 1);
                    logger.LogInformation("Row {Row}: Parsed date = {Date:dd/MM/yyyy} -> Normalized to {Month:yyyy-MM-dd}", row, dateTime, month);
                }
                else
                {
                    logger.LogWarning("Unable to parse date at row {Row}: {Value} (Type: {Type})", row, dateValue, dateValue.GetType().Name);
                    continue;
                }

                var person1SalaryValue = sheet.Cells[row, p1SalaryCol].Value;
                var person2SalaryValue = sheet.Cells[row, p2SalaryCol].Value;
                var expensesValue = sheet.Cells[row, expensesCol].Value;

                if (person1SalaryValue == null && person2SalaryValue == null)
                    continue;

                decimal person1Salary = person1SalaryValue != null ? Convert.ToDecimal(person1SalaryValue) : 0;
                decimal person2Salary = person2SalaryValue != null ? Convert.ToDecimal(person2SalaryValue) : 0;
                decimal totalExpenses = expensesValue != null ? Convert.ToDecimal(expensesValue) : 0;

                decimal person1PercentM1 = 0.5m;
                decimal person2PercentM1 = 0.5m;

                if (person1PercentM1Col != -1)
                {
                    var p1PercentValue = sheet.Cells[row, person1PercentM1Col].Value;
                    if (p1PercentValue != null)
                        person1PercentM1 = Convert.ToDecimal(p1PercentValue);
                }

                if (person2PercentM1Col != -1)
                {
                    var p2PercentValue = sheet.Cells[row, person2PercentM1Col].Value;
                    if (p2PercentValue != null)
                        person2PercentM1 = Convert.ToDecimal(p2PercentValue);
                }

                logger.LogInformation("Row {Row}: Person1Salary={P1Salary}, Person2Salary={P2Salary}, Expenses={Expenses}, P1%M-1={P1Pct}, P2%M-1={P2Pct}", row, person1Salary, person2Salary, totalExpenses, person1PercentM1, person2PercentM1);

                decimal totalSalary = person1Salary + person2Salary;
                decimal person1ExpenseShare = totalSalary > 0 ? (person1Salary / totalSalary) * totalExpenses : totalExpenses / 2;
                decimal person2ExpenseShare = totalSalary > 0 ? (person2Salary / totalSalary) * totalExpenses : totalExpenses / 2;
                decimal person1Savings = person1Salary - person1ExpenseShare;
                decimal person2Savings = person2Salary - person2ExpenseShare;
                decimal person1Ratio = person1Salary > 0 ? person1Savings / person1Salary : 0;
                decimal person2Ratio = person2Salary > 0 ? person2Savings / person2Salary : 0;
                decimal totalSavings = person1Savings + person2Savings;
                decimal totalRatio = totalSalary > 0 ? totalSavings / totalSalary : 0;

                var existingEntry = await db.MonthlyEntries
                    .Include(e => e.SavingsAllocations)
                    .FirstOrDefaultAsync(e => e.Month == month);

                MonthlyEntry monthlyEntry;

                if (existingEntry == null)
                {
                    monthlyEntry = new MonthlyEntry
                    {
                        Month = month,
                        Person1Salary = person1Salary,
                        Person1Expenses = person1ExpenseShare,
                        Person1Savings = person1Savings,
                        Person1SavingsRatio = person1Ratio,
                        Person2Salary = person2Salary,
                        Person2Expenses = person2ExpenseShare,
                        Person2Savings = person2Savings,
                        Person2SavingsRatio = person2Ratio,
                        TotalSalary = totalSalary,
                        TotalExpenses = totalExpenses,
                        TotalSavings = totalSavings,
                        TotalSavingsRatio = totalRatio,
                        PreviousMonthPerson1Percent = person1PercentM1,
                        PreviousMonthPerson2Percent = person2PercentM1
                    };
                    db.MonthlyEntries.Add(monthlyEntry);
                    person1Count++;
                    person2Count++;
                }
                else
                {
                    monthlyEntry = existingEntry;
                    monthlyEntry.Person1Salary = person1Salary;
                    monthlyEntry.Person1Expenses = person1ExpenseShare;
                    monthlyEntry.Person1Savings = person1Savings;
                    monthlyEntry.Person1SavingsRatio = person1Ratio;
                    monthlyEntry.Person2Salary = person2Salary;
                    monthlyEntry.Person2Expenses = person2ExpenseShare;
                    monthlyEntry.Person2Savings = person2Savings;
                    monthlyEntry.Person2SavingsRatio = person2Ratio;
                    monthlyEntry.TotalSalary = totalSalary;
                    monthlyEntry.TotalExpenses = totalExpenses;
                    monthlyEntry.TotalSavings = totalSavings;
                    monthlyEntry.TotalSavingsRatio = totalRatio;
                    monthlyEntry.PreviousMonthPerson1Percent = person1PercentM1;
                    monthlyEntry.PreviousMonthPerson2Percent = person2PercentM1;
                    person1Count++;
                    person2Count++;
                }

                if (monthlyEntry.Id == 0)
                    await db.SaveChangesAsync();

                if (totalSavings > 0)
                    await ImportMonthlyAllocations(sheet, row, monthlyEntry, accountColumns, allAccounts, allRules, totalSavings, person1Owner?.Id, person2Owner?.Id, saveChanges: false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error importing row {Row}", row);
            }
        }

        await db.SaveChangesAsync();
        return (person1Count, person2Count, dbPerson1Name, dbPerson2Name, rulesImported);
    }

    private static int FindColumnIndex(ExcelWorksheet sheet, params string[] searchTerms)
    {
        for (int col = 1; col <= sheet.Dimension.End.Column; col++)
        {
            var headerValue = sheet.Cells[1, col].Value?.ToString()?.ToLower() ?? "";

            foreach (var term in searchTerms)
            {
                if (headerValue.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                    return col;
            }
        }

        return -1;
    }

    private sealed record AccountColumnDefinition(int WeightColumnIndex, string AccountName, int? OwnerId);

    private async Task<List<AccountColumnDefinition>> DetectAccountColumnsAsync(
        ExcelWorksheet sheet,
        string person1Name,
        int? person1OwnerId,
        string person2Name,
        int? person2OwnerId)
    {
        var accountColumns = new List<AccountColumnDefinition>();

        if (sheet.Dimension == null)
            return accountColumns;

        var mergedAddresses = sheet.MergedCells
            .Select(address => sheet.Cells[address])
            .Where(range => range.Start.Row == 1 && range.End.Row == 1)
            .Where(range => (range.End.Column - range.Start.Column + 1) == 2)
            .OrderBy(range => range.Start.Column)
            .ToList();

        foreach (var mergedRange in mergedAddresses)
        {
            var accountName = mergedRange.Text.Trim();
            if (string.IsNullOrWhiteSpace(accountName))
                continue;

            var ownerId = ResolveOwnerIdFromAccountName(accountName, person1Name, person1OwnerId, person2Name, person2OwnerId);
            if (ownerId == null)
            {
                logger.LogWarning("Merged account header '{Header}' does not map to a known owner; skipped", accountName);
                continue;
            }

            accountColumns.Add(new AccountColumnDefinition(mergedRange.Start.Column, accountName, ownerId));
            logger.LogInformation("Detected merged account header '{Header}' on columns {StartCol}-{EndCol} (ownerId: {OwnerId})", accountName, mergedRange.Start.Column, mergedRange.End.Column, ownerId);
        }

        return accountColumns;
    }

    private static int? ResolveOwnerIdFromAccountName(string accountName, string person1Name, int? person1OwnerId, string person2Name, int? person2OwnerId)
    {
        var lastWord = accountName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastWord))
            return null;

        if (person1OwnerId.HasValue && lastWord.Equals(person1Name, StringComparison.OrdinalIgnoreCase))
            return person1OwnerId;

        if (person2OwnerId.HasValue && lastWord.Equals(person2Name, StringComparison.OrdinalIgnoreCase))
            return person2OwnerId;

        return null;
    }

    private async Task<int> ImportAllocationRulesFromRow(ExcelWorksheet sheet, int dataRow, List<AccountColumnDefinition> accountColumns)
    {
        logger.LogInformation("Importing allocation rules from row {Row}", dataRow);

        int rulesImported = 0;

        foreach (var accountColumn in accountColumns)
        {
            decimal weight = 0;
            var weightValue = sheet.Cells[dataRow, accountColumn.WeightColumnIndex].Value;
            if (weightValue != null)
            {
                try { weight = Convert.ToDecimal(weightValue); }
                catch { logger.LogWarning("Could not parse weight value for account {Name}: {Value}", accountColumn.AccountName, weightValue); }
            }

            logger.LogInformation("Processing account {Name} with weight {Weight}", accountColumn.AccountName, weight);

            var savingsAccount = await db.SavingsAccounts
                .FirstOrDefaultAsync(a => a.Name == accountColumn.AccountName);

            if (savingsAccount == null)
            {
                savingsAccount = new SavingsAccount
                {
                    Name = accountColumn.AccountName,
                    OwnerId = accountColumn.OwnerId
                };
                db.SavingsAccounts.Add(savingsAccount);
                await db.SaveChangesAsync();

                logger.LogInformation("Created new savings account: {Name} (ownerId: {OwnerId})", accountColumn.AccountName, accountColumn.OwnerId);
            }
            else
            {
                if (savingsAccount.OwnerId != accountColumn.OwnerId)
                {
                    savingsAccount.OwnerId = accountColumn.OwnerId;
                    logger.LogInformation("Updated ownerId for {Name} to {OwnerId}", accountColumn.AccountName, accountColumn.OwnerId);
                }
            }

            var existingRule = await db.AllocationRules
                .FirstOrDefaultAsync(r => r.SavingsAccountId == savingsAccount.Id);

            if (existingRule == null)
            {
                var newRule = new AllocationRule
                {
                    SavingsAccountId = savingsAccount.Id,
                    Weight = weight
                };
                db.AllocationRules.Add(newRule);
                logger.LogInformation("Created new allocation rule for {Name} with weight {Weight}", accountColumn.AccountName, weight);
                rulesImported++;
            }
            else
            {
                existingRule.Weight = weight;
                logger.LogInformation("Updated allocation rule for {Name} with weight {Weight}", accountColumn.AccountName, weight);
                rulesImported++;
            }
        }

        await db.SaveChangesAsync();
        return rulesImported;
    }

    private async Task ImportMonthlyAllocations(
        ExcelWorksheet sheet,
        int dataRow,
        MonthlyEntry monthlyEntry,
        List<AccountColumnDefinition> accountColumns,
        Dictionary<string, SavingsAccount> accountsCache,
        Dictionary<int, AllocationRule> rulesCache,
        decimal totalSavings,
        int? person1OwnerId,
        int? person2OwnerId,
        bool saveChanges = true)
    {
        logger.LogInformation("Importing allocations for month {Month:yyyy-MM}", monthlyEntry.Month);

        foreach (var accountColumn in accountColumns)
        {
            if (!accountsCache.TryGetValue(accountColumn.AccountName, out var savingsAccount))
            {
                logger.LogWarning("Savings account not found for account name {Name}", accountColumn.AccountName);
                continue;
            }

            var weightValue = sheet.Cells[dataRow, accountColumn.WeightColumnIndex].Value;
            if (weightValue == null)
            {
                logger.LogInformation("No weight value found for {Name} at row {Row}", accountColumn.AccountName, dataRow);
                continue;
            }

            decimal weight = 0;
            try { weight = Convert.ToDecimal(weightValue); }
            catch
            {
                logger.LogWarning("Could not parse weight value for account {Name} at row {Row}: {Value}", accountColumn.AccountName, dataRow, weightValue);
                continue;
            }

            if (weight <= 0)
            {
                logger.LogInformation("Weight is zero or negative for {Name} at row {Row}, skipping", accountColumn.AccountName, dataRow);
                continue;
            }

            decimal ownerPercentM1;
            if (savingsAccount.OwnerId.HasValue && savingsAccount.OwnerId == person1OwnerId)
            {
                ownerPercentM1 = monthlyEntry.PreviousMonthPerson1Percent;
            }
            else if (savingsAccount.OwnerId.HasValue && savingsAccount.OwnerId == person2OwnerId)
            {
                ownerPercentM1 = monthlyEntry.PreviousMonthPerson2Percent;
            }
            else
            {
                logger.LogWarning("Account {Name} has no valid owner (OwnerId={OwnerId}), skipping", accountColumn.AccountName, savingsAccount.OwnerId);
                continue;
            }

            decimal amount = weight * (ownerPercentM1 / 0.5m) * totalSavings;

            if (amount <= 0)
            {
                logger.LogInformation("Calculated amount is zero or negative for {Name} at row {Row}, skipping", accountColumn.AccountName, dataRow);
                continue;
            }

            var existingAllocation = await db.SavingsAllocations
                .FirstOrDefaultAsync(a => a.MonthlyEntryId == monthlyEntry.Id && a.SavingsAccountId == savingsAccount.Id);

            decimal percentage = totalSavings > 0 ? amount / totalSavings : 0;

            // Undo this allocation's previous contribution to the account's transfer accumulator
            // before reapplying, mirroring AllocationCalculationService's recalculation handling.
            if (existingAllocation != null)
                savingsAccount.TransferAccumulator = existingAllocation.AccumulatorBefore;

            decimal accumulatorBefore = savingsAccount.TransferAccumulator;
            decimal transferableAmount;

            if (savingsAccount.TransferThreshold <= 0)
            {
                transferableAmount = amount;
                savingsAccount.TransferAccumulator = 0;
            }
            else
            {
                decimal accumulated = accumulatorBefore + amount;
                if (accumulated >= savingsAccount.TransferThreshold)
                {
                    transferableAmount = accumulated;
                    savingsAccount.TransferAccumulator = 0;
                }
                else
                {
                    transferableAmount = 0;
                    savingsAccount.TransferAccumulator = accumulated;
                }
            }

            if (existingAllocation == null)
            {
                var allocation = new SavingsAllocation
                {
                    MonthlyEntryId = monthlyEntry.Id,
                    SavingsAccountId = savingsAccount.Id,
                    Amount = amount,
                    Percentage = percentage,
                    Weight = weight,
                    AccumulatorBefore = accumulatorBefore,
                    TransferableAmount = transferableAmount
                };
                db.SavingsAllocations.Add(allocation);
                logger.LogInformation("Created allocation: {Name} = {Amount:N2} EUR (weight: {Weight:N4}, ownerPercent M-1: {Pct:N4})", accountColumn.AccountName, amount, weight, ownerPercentM1);
            }
            else
            {
                existingAllocation.Amount = amount;
                existingAllocation.Percentage = percentage;
                existingAllocation.Weight = weight;
                existingAllocation.AccumulatorBefore = accumulatorBefore;
                existingAllocation.TransferableAmount = transferableAmount;
                logger.LogInformation("Updated allocation: {Name} = {Amount:N2} EUR (weight: {Weight:N4}, ownerPercent M-1: {Pct:N4})", accountColumn.AccountName, amount, weight, ownerPercentM1);
            }
        }

        if (saveChanges)
            await db.SaveChangesAsync();
    }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Person1Entries { get; set; }
    public int Person2Entries { get; set; }
    public int AllocationRulesImported { get; set; }
}
