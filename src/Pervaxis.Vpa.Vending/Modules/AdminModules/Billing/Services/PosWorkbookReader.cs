using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

/// <summary>Reads the tabular values used by the billing calculation from a POS .xlsx workbook.</summary>
public sealed class PosWorkbookReader
{
    private const int MaximumTransactions = 50_000;
    private const long MaximumExpandedWorkbookBytes = 50 * 1024 * 1024;

    public PosWorkbookResult Read(Stream workbookStream)
    {
        using var archive = new ZipArchive(workbookStream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Sum(entry => entry.Length) > MaximumExpandedWorkbookBytes)
            throw InvalidWorkbook("The expanded workbook is too large to process.");

        var worksheetEntry = archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (worksheetEntry is null)
            throw InvalidWorkbook("The workbook does not contain a worksheet.");

        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        var sharedStrings = sharedStringsEntry is null
            ? []
            : LoadXml(sharedStringsEntry).Descendants().Where(element => element.Name.LocalName == "si")
                .Select(item => string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)))
                .ToArray();

        var sheet = LoadXml(worksheetEntry);
        var rows = sheet.Descendants().Where(element => element.Name.LocalName == "row").ToArray();
        var header = FindHeader(rows, sharedStrings);
        var indexes = ResolveColumns(header.Values);
        var transactions = new List<BillingImportTransaction>();
        var issues = new List<BillingImportValidationIssue>();
        var rowsWithoutProductName = 0;

        for (var rowIndex = header.Index + 1; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowNumber = GetRowNumber(row, rowIndex + 1);
            var values = ReadRow(row, sharedStrings);
            if (values.Values.All(string.IsNullOrWhiteSpace) || values.Values.Any(value =>
                    string.Equals(value?.Trim(), "total", StringComparison.OrdinalIgnoreCase)))
                continue;

            var productSelectionInfo = Cell(values, indexes.ProductSelectionInfo)?.Trim();
            if (string.IsNullOrWhiteSpace(productSelectionInfo))
            {
                rowsWithoutProductName++;
                continue;
            }

            var transactionId = Cell(values, indexes.TransactionId)?.Trim();
            var settlementText = Cell(values, indexes.SettlementValue)?.Trim();
            var settlementTimeText = Cell(values, indexes.MachineSettlementTime)?.Trim();

            if (string.IsNullOrWhiteSpace(transactionId))
                AddIssue(issues, rowNumber, "Transaction ID", "Transaction ID is required.");
            else if (transactionId.Length > 128)
                AddIssue(issues, rowNumber, "Transaction ID", "Transaction ID must be 128 characters or fewer.");
            if (!TryParseMoney(settlementText, out var settlementValue))
                AddIssue(issues, rowNumber, "Settlement Value", "Settlement Value must be a valid amount.");
            if (!TryParseMachineTime(settlementTimeText, out var settlementTime))
                AddIssue(issues, rowNumber, "Machine Settlement Time", "Machine Settlement Time must be a valid date and time.");

            ValidateLength(issues, rowNumber, "Site ID", Cell(values, indexes.SiteId), 128);
            ValidateLength(issues, rowNumber, "Currency", Cell(values, indexes.Currency), 8);
            ValidateLength(issues, rowNumber, "Machine Name", Cell(values, indexes.MachineName), 255);
            ValidateLength(issues, rowNumber, "Product Selection Info", productSelectionInfo, 1000);

            if (issues.Any(issue => issue.RowNumber == rowNumber))
                continue;

            transactions.Add(new BillingImportTransaction
            {
                SourceRowNumber = rowNumber,
                TransactionId = transactionId!,
                SiteId = Cell(values, indexes.SiteId)?.Trim(),
                Currency = Cell(values, indexes.Currency)?.Trim(),
                MachineName = Cell(values, indexes.MachineName)?.Trim(),
                SettlementValue = settlementValue,
                MachineSettlementTime = settlementTime,
                ProductSelectionInfo = string.IsNullOrWhiteSpace(productSelectionInfo) ? null : productSelectionInfo,
            });

            if (transactions.Count > MaximumTransactions)
                throw InvalidWorkbook($"A workbook may contain at most {MaximumTransactions:N0} transactions.");
        }

        if (transactions.Count == 0 && issues.Count == 0)
            AddIssue(issues, null, "Workbook", "No transaction rows were found below the header row.");

        return new PosWorkbookResult(transactions, issues, rowsWithoutProductName);
    }

    private static HeaderRow FindHeader(IReadOnlyList<XElement> rows, IReadOnlyList<string> sharedStrings)
    {
        for (var rowIndex = 0; rowIndex < Math.Min(rows.Count, 30); rowIndex++)
        {
            var row = rows[rowIndex];
            var values = ReadRow(row, sharedStrings);
            var normalized = values.ToDictionary(pair => pair.Key, pair => NormalizeHeader(pair.Value));
            if (normalized.Values.Contains("transactionid")
                && normalized.Values.Contains("machinesettlementtime")
                && normalized.Values.Contains("productselectioninfo")
                && normalized.Values.Any(IsSettlementHeader))
                return new HeaderRow(rowIndex, GetRowNumber(row, rowIndex + 1), normalized);
        }

        throw InvalidWorkbook("Could not find the POS transaction header row. Required columns are Transaction ID, Settlement Value (Vend Price), Machine Settlement Time, and Product Selection Info.");
    }

    private static ColumnIndexes ResolveColumns(IReadOnlyDictionary<int, string> headers)
    {
        int? Find(params string[] names) => headers
            .Where(pair => names.Contains(pair.Value, StringComparer.OrdinalIgnoreCase))
            .Select(pair => (int?)pair.Key)
            .FirstOrDefault();

        var transactionId = Find("transactionid");
        var settlement = headers.Where(pair => IsSettlementHeader(pair.Value)).Select(pair => (int?)pair.Key).FirstOrDefault();
        var settlementTime = Find("machinesettlementtime");
        var productSelection = Find("productselectioninfo");
        if (transactionId is null || settlement is null || settlementTime is null || productSelection is null)
            throw InvalidWorkbook("The POS workbook is missing one or more required columns.");

        return new ColumnIndexes(
            transactionId.Value,
            settlement.Value,
            settlementTime.Value,
            productSelection.Value,
            Find("siteid"),
            Find("currency"),
            Find("machinename"));
    }

    private static bool IsSettlementHeader(string header) => header is
        "settlementvaluevendprice" or "settlementvalue" or "settlement";

    private static Dictionary<int, string?> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<int, string?>();
        var nextColumnIndex = 0;
        foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
        {
            var reference = (string?)cell.Attribute("r");
            var index = string.IsNullOrWhiteSpace(reference) ? nextColumnIndex : ColumnIndex(reference);
            var type = (string?)cell.Attribute("t");
            string? value;
            if (type == "inlineStr")
            {
                value = string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
            }
            else
            {
                value = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
                if (type == "s" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var stringIndex))
                    value = stringIndex >= 0 && stringIndex < sharedStrings.Count ? sharedStrings[stringIndex] : null;
            }
            values[index] = value;
            nextColumnIndex = index + 1;
        }
        return values;
    }

    private static string? Cell(IReadOnlyDictionary<int, string?> values, int? index) =>
        index is not null && values.TryGetValue(index.Value, out var value) ? value : null;

    private static string NormalizeHeader(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
                break;
            index = checked(index * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        return index - 1;
    }

    private static int GetRowNumber(XElement row, int fallbackRowNumber) =>
        int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallbackRowNumber;

    private static bool TryParseMoney(string? value, out decimal amount)
    {
        var cleaned = value?.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
            CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryParseMachineTime(string? value, out DateTime machineTime)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var excelSerial))
        {
            try
            {
                machineTime = DateTime.SpecifyKind(DateTime.FromOADate(excelSerial), DateTimeKind.Unspecified);
                return true;
            }
            catch (ArgumentException)
            {
                // Continue with text parsing so the caller receives a field-specific validation error.
            }
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out machineTime))
        {
            machineTime = DateTime.SpecifyKind(machineTime, DateTimeKind.Unspecified);
            return true;
        }

        machineTime = default;
        return false;
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumExpandedWorkbookBytes)
            throw InvalidWorkbook("A workbook component is too large to process.");

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumExpandedWorkbookBytes,
        });
        return XDocument.Load(reader);
    }

    private static void AddIssue(List<BillingImportValidationIssue> issues, int? rowNumber, string field, string message)
    {
        if (issues.Count < 100)
            issues.Add(new BillingImportValidationIssue(rowNumber, field, message));
    }

    private static void ValidateLength(
        List<BillingImportValidationIssue> issues,
        int rowNumber,
        string field,
        string? value,
        int maximumLength)
    {
        if (value?.Length > maximumLength)
            AddIssue(issues, rowNumber, field, $"{field} must be {maximumLength} characters or fewer.");
    }

    private static InvalidPosWorkbookException InvalidWorkbook(string message) =>
        new([new BillingImportValidationIssue(null, "Workbook", message)]);

    private sealed record HeaderRow(int Index, int RowNumber, IReadOnlyDictionary<int, string> Values);

    private sealed record ColumnIndexes(
        int TransactionId,
        int SettlementValue,
        int MachineSettlementTime,
        int ProductSelectionInfo,
        int? SiteId,
        int? Currency,
        int? MachineName);
}

public sealed record PosWorkbookResult(
    IReadOnlyList<BillingImportTransaction> Transactions,
    IReadOnlyList<BillingImportValidationIssue> Issues,
    int RowsWithoutProductName);

public sealed class InvalidPosWorkbookException(IReadOnlyList<BillingImportValidationIssue> issues) : Exception("The POS workbook is invalid.")
{
    public IReadOnlyList<BillingImportValidationIssue> Issues { get; } = issues;
}
