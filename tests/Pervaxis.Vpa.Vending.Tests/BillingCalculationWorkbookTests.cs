using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Ambeeka.Calculation;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;
using Xunit;

namespace Pervaxis.Vpa.Vending.Tests;

public sealed class BillingCalculationWorkbookTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WorkbookContainsBothSheetsAndEveryWeekWithLiteralNamesAndNumericAmounts(bool includeRows)
    {
        var date = new DateOnly(2026, 11, 2);
        var totals = new BillingPeriodTotals(0, 0, 0, 0, 0, null);
        var weeks = Enumerable.Range(0, 2).Select(i => new WeeklyBillingResult(
            date.AddDays(i * 7), date.AddDays(i * 7 + 6),
            includeRows ? [new CalculatedBillingRow($"c{i}", date.ToDateTime(new TimeOnly(9, 30)),
                "=SUM(1,2) & <product> 🍫", "Matched", 3.25m, 2.25m, 1m, 0m, true)] : [],
            includeRows ? [new UnmatchedBillingRow($"u{i}", date.ToDateTime(TimeOnly.MinValue),
                "Unknown", 1.75m, UnmatchedProductReason.AliasNotFound)] : [],
            0, totals)).ToArray();
        var snapshot = new BillingCalculationResponse(Guid.NewGuid(), "Test calculation", "Employer", "NeedsReview",
            DateTimeOffset.UtcNow, new MonthlyBillingResult(2026, 11, weeks, 0, totals, false, []));

        using var stream = new MemoryStream(BillingCalculationWorkbook.Create(snapshot));
        using var archive = new ZipArchive(stream);
        // Every package XML part must be well formed.
        foreach (var entry in archive.Entries)
        {
            using var part = entry.Open();
            XDocument.Load(part);
        }
        var workbook = Read("xl/workbook.xml");
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        Assert.All(Read("xl/_rels/workbook.xml.rels").Descendants(relationships + "Relationship"),
            relationship => Assert.StartsWith("http://schemas.openxmlformats.org/officeDocument/2006/relationships/",
                (string?)relationship.Attribute("Type")));
        Assert.Equal(new[] { "Calculated Data", "Unmatched Products" },
            workbook.Descendants(Ns + "sheet").Select(sheet => (string?)sheet.Attribute("name")));
        var calculated = Read("xl/worksheets/sheet1.xml");
        var unmatched = Read("xl/worksheets/sheet2.xml");
        Assert.Equal(includeRows ? 3 : 1, calculated.Descendants(Ns + "row").Count());
        Assert.Equal(includeRows ? 3 : 1, unmatched.Descendants(Ns + "row").Count());
        Assert.Empty(calculated.Descendants(Ns + "f"));
        Assert.Equal("Week start", calculated.Descendants(Ns + "row").First().Elements(Ns + "c").First().Value);
        Assert.Equal("Week start", unmatched.Descendants(Ns + "row").First().Elements(Ns + "c").First().Value);
        if (includeRows)
        {
            var row = calculated.Descendants(Ns + "row").Skip(1).First().Elements(Ns + "c").ToArray();
            Assert.Equal("=SUM(1,2) & <product> 🍫", row[3].Value);
            Assert.Equal("inlineStr", (string?)row[3].Attribute("t"));
            Assert.Equal(3.25m, decimal.Parse(row[5].Element(Ns + "v")!.Value, CultureInfo.InvariantCulture));
            Assert.Equal("Review", row[9].Value);
            Assert.Contains("2026-11-09", calculated.Descendants(Ns + "t").Select(t => t.Value));
            Assert.Contains("AliasNotFound", unmatched.Descendants(Ns + "t").Select(t => t.Value));
        }

        XDocument Read(string name)
        {
            using var part = archive.GetEntry(name)!.Open();
            return XDocument.Load(part);
        }
    }
}
