namespace HR.Employee.API.Application.Imports;

using System.Globalization;
using ClosedXML.Excel;

/// <summary>Template mein dropdown: column ke liye allowed values (seedhi list ya "Lists" sheet ka column).</summary>
public sealed record ImportChoiceList(string ColumnKey, string Title, IReadOnlyList<string> Values);

/// <summary>
/// .xlsx padhna aur template banana (ClosedXML, MIT). Sheet "Data" (ya pehli sheet) ki row 1 header hai.
/// Header match: lower-case, sirf harf/number — "Work email *" = "workemail".
/// </summary>
public static class ImportWorkbook
{
    public const int MaxRows = 10_000;
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const string DataSheet = "Data";

    public static string HeaderKey(string header)
        => new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>File → rows. File-level ghalti (kharab file, column ghayab, bohat rows) errors mein row 1 pe.</summary>
    public static IReadOnlyList<ImportRow> Read(Stream file, IReadOnlyList<ImportColumn> columns, ImportErrors errors)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(file);
        }
        catch (Exception)
        {
            errors.Add(1, null, null, ImportErrorCodes.BadFile, "This is not a valid .xlsx file. Download the template and fill it in.");
            return [];
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals(DataSheet, StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheets.First();

            var used = sheet.RangeUsed();
            if (used is null)
            {
                errors.Add(1, null, null, ImportErrorCodes.EmptyFile, "The file has no data rows.");
                return [];
            }

            // Header → column number
            var map = new Dictionary<string, int>();
            var lastColumn = used.LastColumn().ColumnNumber();
            for (var c = 1; c <= lastColumn; c++)
            {
                var header = sheet.Cell(1, c).GetString();
                var key = HeaderKey(header);
                if (key.Length > 0)
                    map.TryAdd(key, c);
            }

            var missing = columns.Where(col => col.Required && !map.ContainsKey(HeaderKey(col.Header))).ToList();
            foreach (var col in missing)
                errors.Add(1, col.Header, null, ImportErrorCodes.MissingColumn, $"Column '{col.Header}' is missing. Use the template.",
                           ImportText.Args(("column", col.Header)));
            if (missing.Count > 0)
                return [];

            var positions = columns
                .Select(col => (col.Key, Index: map.TryGetValue(HeaderKey(col.Header), out var i) ? i : 0))
                .Where(x => x.Index > 0)
                .ToList();

            var rows = new List<ImportRow>();
            var lastRow = used.LastRow().RowNumber();
            for (var r = 2; r <= lastRow; r++)
            {
                var values = new Dictionary<string, string?>();
                foreach (var (key, index) in positions)
                    values[key] = CellText(sheet.Cell(r, index));

                if (values.Values.All(string.IsNullOrWhiteSpace))
                    continue;   // khali row chhor do

                if (rows.Count == MaxRows)
                {
                    errors.Add(r, null, null, ImportErrorCodes.TooManyRows, $"A file can have at most {MaxRows:N0} rows. Split it into smaller files.",
                               ImportText.Args(("max", MaxRows)));
                    return [];
                }

                rows.Add(new ImportRow(r, values));
            }

            if (rows.Count == 0)
                errors.Add(1, null, null, ImportErrorCodes.EmptyFile, "The file has no data rows.");

            return rows;
        }
    }

    /// <summary>Date cell → yyyy-MM-dd, number → invariant (1.0 → "1"), baqi text jaisa.</summary>
    private static string? CellText(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Number => value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "Yes" : "No",
            XLDataType.Error => null,
            _ => cell.GetString()
        };
    }

    /// <summary>
    /// Template: "Data" sheet (sirf headers + dropdowns), "Instructions" (har column ka matlab + misaal),
    /// "Lists" (company ke maujooda codes, dropdown inhi se).
    /// </summary>
    public static byte[] Template(ImportKind kind, IReadOnlyList<ImportColumn> columns, IReadOnlyList<ImportChoiceList> lists)
    {
        using var workbook = new XLWorkbook();
        var data = workbook.AddWorksheet(DataSheet);
        var help = workbook.AddWorksheet("Instructions");
        var listSheet = lists.Count > 0 ? workbook.AddWorksheet("Lists") : null;

        var ink = XLColor.FromHtml("#13211A");
        var brass = XLColor.FromHtml("#B08D3E");
        var vellum = XLColor.FromHtml("#F6F1E4");

        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var cell = data.Cell(1, i + 1);
            cell.Value = col.Required ? $"{col.Header} *" : col.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = col.Required ? XLColor.White : ink;
            cell.Style.Fill.BackgroundColor = col.Required ? ink : vellum;
            cell.CreateComment().AddText($"{(col.Required ? "Required. " : "Optional. ")}{col.Format}");
            data.Column(i + 1).Width = col.Width;

            // Dates aur codes text rahen (Excel 00012 ko 12 na bana de); date columns date format
            var body = data.Range(2, i + 1, MaxRows + 1, i + 1);
            if (col.Format.Contains("date", StringComparison.OrdinalIgnoreCase))
                body.Style.NumberFormat.Format = "yyyy-mm-dd";
            else
                body.Style.NumberFormat.Format = "@";
        }
        data.SheetView.FreezeRows(1);
        data.Row(1).Height = 22;

        // Dropdowns (warning style: user list se bahar likh sakta hai, server phir bhi check karega)
        if (listSheet is not null)
        {
            for (var l = 0; l < lists.Count; l++)
            {
                var list = lists[l];
                var header = listSheet.Cell(1, l + 1);
                header.Value = list.Title;
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = vellum;
                for (var v = 0; v < list.Values.Count; v++)
                    listSheet.Cell(v + 2, l + 1).Value = list.Values[v];
                listSheet.Column(l + 1).Width = 28;

                var index = columns.ToList().FindIndex(c => c.Key == list.ColumnKey);
                if (index < 0 || list.Values.Count == 0)
                    continue;

                var validation = data.Range(2, index + 1, MaxRows + 1, index + 1).CreateDataValidation();
                validation.List(listSheet.Range(2, l + 1, list.Values.Count + 1, l + 1), true);
                validation.ErrorStyle = XLErrorStyle.Warning;
                validation.ErrorTitle = "Not in the list";
                validation.ErrorMessage = $"Pick a value from the list ({list.Title}).";
            }
            listSheet.SheetView.FreezeRows(1);
        }

        // Instructions
        help.Cell(1, 1).Value = $"Bulk import: {kind}";
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(1, 1).Style.Font.FontSize = 14;
        help.Cell(2, 1).Value =
            $"Fill the 'Data' sheet, one record per row (max {MaxRows:N0}). Columns marked * are required. " +
            "The whole file is checked first; if any row has a problem nothing is saved, so fix the listed rows and upload again. " +
            "Duplicates are checked ignoring case and extra spaces, inside the file and against records already in the system.";
        help.Range(2, 1, 2, 4).Merge().Style.Alignment.WrapText = true;
        help.Row(2).Height = 48;

        string[] heads = ["Column", "Required", "Format / allowed values", "Example"];
        for (var h = 0; h < heads.Length; h++)
        {
            var cell = help.Cell(4, h + 1);
            cell.Value = heads[h];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = ink;
        }
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            help.Cell(i + 5, 1).Value = col.Header;
            help.Cell(i + 5, 2).Value = col.Required ? "Yes" : "No";
            help.Cell(i + 5, 3).Value = col.Format;
            help.Cell(i + 5, 4).SetValue(col.Example);
            if (col.Required)
                help.Cell(i + 5, 2).Style.Font.FontColor = brass;
        }
        help.Column(1).Width = 26;
        help.Column(2).Width = 10;
        help.Column(3).Width = 70;
        help.Column(4).Width = 30;
        help.Column(3).Style.Alignment.WrapText = true;

        data.SetTabActive();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
