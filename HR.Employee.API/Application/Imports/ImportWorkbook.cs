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

            // Header → column number. Columns kisi bhi tarteeb mein hon, naam se milte hain.
            // Ghalat spelling / anjaan column / ek column do baar = file reject (warna data chupke se chhoot jata).
            var known = columns.ToDictionary(col => HeaderKey(col.Header), col => col.Header);
            var map = new Dictionary<string, int>();
            var headerErrors = 0;
            var lastColumn = used.LastColumn().ColumnNumber();
            for (var c = 1; c <= lastColumn; c++)
            {
                var header = sheet.Cell(1, c).GetString().Trim();
                var key = HeaderKey(header);
                if (key.Length == 0)
                    continue;

                if (!known.TryGetValue(key, out var name))
                {
                    var guess = Closest(key, known);
                    errors.Add(1, header, header, ImportErrorCodes.UnknownColumn,
                               guess is null
                                   ? $"Column '{header}' is not in the template. Rename or remove it."
                                   : $"Column '{header}' is not in the template. Did you mean '{guess}'?",
                               ImportText.Args(("column", header), ("suggestion", guess ?? "")));
                    headerErrors++;
                }
                else if (!map.TryAdd(key, c))
                {
                    errors.Add(1, name, header, ImportErrorCodes.DuplicateColumn, $"Column '{name}' appears more than once. Keep only one.",
                               ImportText.Args(("column", name)));
                    headerErrors++;
                }
            }

            foreach (var col in columns.Where(col => col.Required && !map.ContainsKey(HeaderKey(col.Header))))
            {
                errors.Add(1, col.Header, null, ImportErrorCodes.MissingColumn, $"Column '{col.Header}' is missing. Use the template.",
                           ImportText.Args(("column", col.Header)));
                headerErrors++;
            }
            if (headerErrors > 0)
                return [];

            var headers = columns.ToDictionary(col => col.Key, col => col.Header);

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
                {
                    var cell = sheet.Cell(r, index);
                    if (cell.Value.Type == XLDataType.Error)
                    {
                        errors.Add(r, headers[key], cell.Value.GetError().ToString(), ImportErrorCodes.CellError,
                                   "This cell has an Excel error (like #N/A). Type the value instead of a formula.");
                        values[key] = null;
                        continue;
                    }

                    var text = CellText(cell);
                    if (text is not null && NullWords.Contains(text.Trim()))
                    {
                        // "null"/"N/A" text DB mein naam/code ban kar na jaye — khali chhorna hai to cell khali chhoren
                        errors.Add(r, headers[key], text.Trim(), ImportErrorCodes.NullText,
                                   $"'{text.Trim()}' is not a value. Leave the cell empty instead.",
                                   ImportText.Args(("value", text.Trim())));
                        text = null;
                    }
                    values[key] = text;
                }

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

    /// <summary>Placeholder text jo asal value nahi (case ignore). "NA" nahi — wo Namibia ka country code hai.</summary>
    private static readonly HashSet<string> NullWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "null", "(null)", "nil", "none", "undefined", "n/a", "#n/a", "nan", "#null!", "-", "--"
    };

    /// <summary>Ghalat spelling wale header ke liye sab se qareeb template column (max 3 harf ka farq).</summary>
    private static string? Closest(string key, IReadOnlyDictionary<string, string> known)
    {
        var best = known.Select(k => (Name: k.Value, Distance: Distance(key, k.Key)))
                        .OrderBy(x => x.Distance)
                        .FirstOrDefault();
        return best.Name is not null && best.Distance <= Math.Max(1, Math.Min(3, key.Length / 3)) ? best.Name : null;
    }

    private static int Distance(string a, string b)
    {
        var prev = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var cur = new int[b.Length + 1];
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            prev = cur;
        }
        return prev[b.Length];
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
