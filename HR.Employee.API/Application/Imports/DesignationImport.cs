namespace HR.Employee.API.Application.Imports;

using System.Globalization;

/// <summary>Designations: title unique (case/space ignore), level 1–20 optional.</summary>
public static class DesignationImport
{
    public static readonly ImportColumn Title = new("title", "Title", true, "Up to 150 characters, unique.", "Senior Accountant", 28);
    public static readonly ImportColumn Level = new("level", "Level", false, "Whole number 1 (entry) to 20 (C-level).", "6", 10);
    public static readonly ImportColumn Description = new("description", "Description", false, "Up to 500 characters.", "Leads month-end close", 34);

    public static readonly IReadOnlyList<ImportColumn> Columns = [Title, Level, Description];

    public sealed record Planned(int Row, string Title, byte? Level, string? Description);

    public static List<Planned> Plan(IReadOnlyList<ImportRow> rows, IReadOnlyCollection<string> existingTitles, ImportErrors errors)
    {
        var titles = new UniqueKeys("Designation", existingTitles);
        var plan = new List<Planned>();

        foreach (var row in rows)
        {
            var c = new ImportCells(row, errors);
            var title = c.Text(Title, 150);
            titles.Claim(c, Title, title);

            byte? level = null;
            if (c.Text(Level, 10) is { } text)
            {
                if (byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var l) && l is >= 1 and <= 20)
                    level = l;
                else
                    c.Fail(Level, ImportErrorCodes.InvalidNumber, "Level must be a whole number from 1 to 20.", ImportText.Args(("min", 1), ("max", 20)));
            }

            var description = c.Text(Description, 500);
            if (!errors.RowHasErrors(row.Number))
                plan.Add(new Planned(row.Number, title!, level, description));
        }

        return plan;
    }
}
