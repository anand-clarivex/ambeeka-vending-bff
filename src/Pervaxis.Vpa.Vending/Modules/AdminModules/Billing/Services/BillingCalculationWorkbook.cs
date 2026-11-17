using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

/// <summary>Writes the saved snapshot as a two-worksheet Excel workbook without recalculating billing.</summary>
public static class BillingCalculationWorkbook
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Create(BillingCalculationResponse snapshot)
    {
        XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", new XElement(types + "Types",
                new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                Override("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                Override("/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"),
                Override("/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
                Override("/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            Write(archive, "_rels/.rels", new XElement(Relationships + "Relationships",
                Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            Write(archive, "xl/workbook.xml", new XElement(Spreadsheet + "workbook",
                new XElement(Spreadsheet + "sheets",
                    Sheet("Calculated Data", 1), Sheet("Unmatched Products", 2))));
            Write(archive, "xl/_rels/workbook.xml.rels", new XElement(Relationships + "Relationships",
                Relationship("rId1", "worksheet", "worksheets/sheet1.xml"),
                Relationship("rId2", "worksheet", "worksheets/sheet2.xml"),
                Relationship("rId3", "styles", "styles.xml")));
            Write(archive, "xl/styles.xml", new XElement(Spreadsheet + "styleSheet",
                new XElement(Spreadsheet + "fonts", new XAttribute("count", 1), new XElement(Spreadsheet + "font")),
                new XElement(Spreadsheet + "fills", new XAttribute("count", 2),
                    new XElement(Spreadsheet + "fill", new XElement(Spreadsheet + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(Spreadsheet + "fill", new XElement(Spreadsheet + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(Spreadsheet + "borders", new XAttribute("count", 1), new XElement(Spreadsheet + "border")),
                new XElement(Spreadsheet + "cellStyleXfs", new XAttribute("count", 1), Format(0)),
                new XElement(Spreadsheet + "cellXfs", new XAttribute("count", 2), Format(0), Format(4))));

            WriteSheet(archive, 1,
                ["Week start", "Week end", "Settlement time", "Source product", "Matched product", "Price", "Free item", "Net price", "Settlement", "Review"],
                snapshot.Result.Weeks.SelectMany(week => week.Rows.Select(row => new object[]
                {
                    week.StartDate, week.EndDate, row.MachineSettlementTime, row.SourceProductName, row.ProductName,
                    row.Price, row.FreeItem, row.NetPrice, row.Settlement, row.HighlightForReview ? "Review" : "—",
                })));
            WriteSheet(archive, 2,
                ["Week start", "Week end", "Settlement time", "Source product", "Settlement", "Reason"],
                snapshot.Result.Weeks.SelectMany(week => week.UnmatchedRows.Select(row => new object[]
                {
                    week.StartDate, week.EndDate, row.MachineSettlementTime, row.SourceProductName, row.Settlement, row.Reason.ToString(),
                })));
        }
        return output.ToArray();

        XElement Override(string part, string contentType) => new(types + "Override",
            new XAttribute("PartName", part), new XAttribute("ContentType", contentType));
    }

    private static XElement Sheet(string name, int id) => new(Spreadsheet + "sheet",
        new XAttribute("name", name), new XAttribute("sheetId", id), new XAttribute(OfficeRelationships + "id", $"rId{id}"));

    private static XElement Relationship(string id, string type, string target) => new(Relationships + "Relationship",
        new XAttribute("Id", id), new XAttribute("Type", OfficeRelationships.NamespaceName + "/" + type), new XAttribute("Target", target));

    private static XElement Format(int numberFormat) => new(Spreadsheet + "xf",
        new XAttribute("numFmtId", numberFormat), new XAttribute("fontId", 0),
        new XAttribute("fillId", 0), new XAttribute("borderId", 0));

    private static void WriteSheet(ZipArchive archive, int id, string[] headers, IEnumerable<object[]> rows)
    {
        using var stream = archive.CreateEntry($"xl/worksheets/sheet{id}.xml").Open();
        using var writer = XmlWriter.Create(stream);
        writer.WriteStartElement("worksheet", Spreadsheet.NamespaceName);
        writer.WriteStartElement("cols");
        writer.WriteStartElement("col");
        writer.WriteAttributeString("min", "1");
        writer.WriteAttributeString("max", headers.Length.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", "24");
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        WriteRow(headers.Cast<object>().ToArray());
        foreach (var row in rows) WriteRow(row);
        writer.WriteEndElement();
        writer.WriteEndElement();

        void WriteRow(object[] values)
        {
            writer.WriteStartElement("row");
            foreach (var value in values)
            {
                writer.WriteStartElement("c");
                if (value is decimal amount)
                {
                    writer.WriteAttributeString("s", "1");
                    writer.WriteElementString("v", amount.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    // Inline strings keep product names literal, including names starting with '='.
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is");
                    writer.WriteStartElement("t");
                    writer.WriteAttributeString("xml", "space", null, "preserve");
                    var text = value switch
                    {
                        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        _ => value.ToString() ?? "",
                    };
                    writer.WriteString(string.Concat(text.EnumerateRunes()
                        .Where(rune => rune.Value is 0x9 or 0xA or 0xD
                            || rune.Value is >= 0x20 and <= 0xD7FF
                            || rune.Value is >= 0xE000 and <= 0xFFFD
                            || rune.Value is >= 0x10000 and <= 0x10FFFF)
                        .Select(rune => rune.ToString())));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
    }

    private static void Write(ZipArchive archive, string path, XElement element)
    {
        using var stream = archive.CreateEntry(path).Open();
        element.Save(stream);
    }
}
