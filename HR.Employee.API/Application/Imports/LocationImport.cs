namespace HR.Employee.API.Application.Imports;

using HR.Employee.API.Domain.Organization;

/// <summary>Locations (office/branch) ki sheet: columns, checks aur plan. DB ka kaam ImportHandlers mein.</summary>
public static class LocationImport
{
    public static readonly ImportColumn Code = new("code", "Code", true, "Up to 20 letters/digits, unique (saved in capitals).", "KHI-HO", 14);
    public static readonly ImportColumn Name = new("name", "Name", true, "Up to 150 characters, unique.", "Karachi Head Office", 28);
    public static readonly ImportColumn Country = new("country", "Country", true, "2-letter ISO country code.", "PK", 10);
    public static readonly ImportColumn TimeZone = new("timezone", "Time zone", true, "IANA time zone id.", "Asia/Karachi", 20);
    public static readonly ImportColumn City = new("city", "City", false, "Up to 100 characters.", "Karachi", 16);
    public static readonly ImportColumn Address = new("address", "Address", false, "Up to 300 characters.", "Plot 12, Shahrah-e-Faisal", 30);
    public static readonly ImportColumn WorkWeek = new("workweek", "Work week", false, "Working days, e.g. Mon-Fri, Mon-Sat or Sun-Thu. Blank = Mon-Fri.", "Mon-Fri", 14);
    public static readonly ImportColumn HeadOffice = new("headoffice", "Head office", false, "Yes or No (blank = No). Only one head office.", "Yes", 12);

    public static readonly IReadOnlyList<ImportColumn> Columns = [Code, Name, Country, TimeZone, City, Address, WorkWeek, HeadOffice];

    public sealed record Existing(string Code, string Name);

    public sealed record Planned(
        int Row, string Code, string Name, string CountryCode, string TimeZone,
        string? City, string? Address, byte WorkWeekDays, bool IsHeadOffice);

    public static List<Planned> Plan(IReadOnlyList<ImportRow> rows, IReadOnlyCollection<Existing> existing, ImportErrors errors)
    {
        var codes = new UniqueKeys("Location code", existing.Select(x => x.Code));
        var names = new UniqueKeys("Location name", existing.Select(x => x.Name));
        var plan = new List<Planned>();
        int? headOfficeRow = null;

        foreach (var row in rows)
        {
            var c = new ImportCells(row, errors);
            var code = c.Code(Code);
            var name = c.Text(Name, 150);
            codes.Claim(c, Code, code);
            names.Claim(c, Name, name);

            var country = c.Text(Country, 10)?.ToUpperInvariant();
            if (country is not null && (country.Length != 2 || !country.All(char.IsAsciiLetterUpper)))
            {
                c.Fail(Country, ImportErrorCodes.InvalidCountry, "Country must be a 2-letter ISO code like PK or AE.");
                country = null;
            }

            var timeZone = c.Text(TimeZone, 64);
            if (timeZone is not null && !TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
            {
                c.Fail(TimeZone, ImportErrorCodes.InvalidTimeZone, "Unknown time zone. Use an IANA id like Asia/Karachi.");
                timeZone = null;
            }

            byte workWeek = Location.MondayToFriday;
            if (c.Text(WorkWeek, 60) is { } days)
            {
                if (ImportText.TryWorkWeek(days) is { } mask)
                    workWeek = mask;
                else
                    c.Fail(WorkWeek, ImportErrorCodes.InvalidWorkWeek, "Work week must list days like Mon-Fri or Mon,Tue,Wed,Thu,Fri,Sat.");
            }

            var city = c.Text(City, 100);
            var address = c.Text(Address, 300);
            var isHeadOffice = c.YesNo(HeadOffice);
            if (isHeadOffice)
            {
                if (headOfficeRow is { } first)
                    c.Fail(HeadOffice, ImportErrorCodes.MultipleHeadOffices, $"Only one head office is allowed (row {first} is already marked).",
                           ImportText.Args(("row", first)));
                else
                    headOfficeRow = row.Number;
            }

            if (!errors.RowHasErrors(row.Number))
                plan.Add(new Planned(row.Number, code!, name!, country!, timeZone!, city, address, workWeek, isHeadOffice));
        }

        return plan;
    }
}
