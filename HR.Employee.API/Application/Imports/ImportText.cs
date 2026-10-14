namespace HR.Employee.API.Application.Imports;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Duplicate pakadne ki chabi: Trim + ToLowerInvariant + beech ke double space ek.
/// " Ali  Hassan " aur "ali hassan" ek hi shakhs hain.
/// </summary>
public static class ImportText
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex CodePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._/\-]*$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex NamePattern = new(@"^[\p{L}\p{M}][\p{L}\p{M}' .\-]*$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9][0-9 ()\-]{5,19}$", RegexOptions.CultureInvariant, RegexTimeout);

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMM yyyy", "d MMM yyyy"];

    /// <summary>Compare/duplicate key. Null rahe to null.</summary>
    public static string? Key(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Spaces.Replace(value.Trim(), " ").ToLowerInvariant();

    /// <summary>Save hone wali value: trim + beech ka extra space hata do (case wahi rehta hai).</summary>
    public static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : Spaces.Replace(value.Trim(), " ");

    public static bool IsCode(string value) => CodePattern.IsMatch(value);
    public static bool IsName(string value) => NamePattern.IsMatch(value);
    public static bool IsEmail(string value) => EmailPattern.IsMatch(value);
    public static bool IsPhone(string value) => PhonePattern.IsMatch(value);

    /// <summary>Excel date cell (reader yyyy-MM-dd bana deta hai), Excel serial number, ya likhi hui date.</summary>
    public static bool TryDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            return true;

        // General format cell mein date ka serial number (45292 = 2024-01-01)
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and < 2958466)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(serial)));
            return true;
        }

        return false;
    }

    public static bool? TryYesNo(string value) => Key(value) switch
    {
        "yes" or "y" or "true" or "1" => true,
        "no" or "n" or "false" or "0" => false,
        _ => null
    };

    private static readonly string[] DayNames = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

    /// <summary>"Mon-Fri", "Mon,Tue,Wed,Thu,Fri,Sat", "Sun-Thu" → bitmask (Mon=1 … Sun=64). Ghalat ho to null.</summary>
    public static byte? TryWorkWeek(string value)
    {
        byte mask = 0;
        foreach (var raw in value.Split([',', ';', '/', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim().ToLowerInvariant();
            var range = part.Split('-', 2);
            var from = DayIndex(range[0]);
            var to = range.Length == 2 ? DayIndex(range[1]) : from;
            if (from < 0 || to < 0)
                return null;

            // Sun-Thu jaisa range hafte ke aakhir se ghoom kar aata hai
            for (var i = from; ; i = (i + 1) % 7)
            {
                mask |= (byte)(1 << i);
                if (i == to)
                    break;
            }
        }
        return mask == 0 ? null : mask;
    }

    private static int DayIndex(string text)
        => text.Length >= 3 ? Array.IndexOf(DayNames, text[..3]) : -1;

    public static IReadOnlyDictionary<string, string> Args(params (string Key, object Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => Convert.ToString(p.Value, CultureInfo.InvariantCulture) ?? "");
}

/// <summary>Ek row ke cells padhta hai aur har ghalti ImportErrors mein likhta hai. Ghalat value pe null lautata hai.</summary>
public sealed class ImportCells(ImportRow row, ImportErrors errors)
{
    public ImportRow Row => row;

    public string? Text(ImportColumn column, int maxLength)
    {
        var value = ImportText.Clean(row[column.Key]);
        if (value is null)
        {
            if (column.Required)
                errors.Add(row, column, ImportErrorCodes.Required, $"{column.Header} is required.");
            return null;
        }
        if (value.Length > maxLength)
        {
            errors.Add(row, column, ImportErrorCodes.TooLong, $"{column.Header} cannot exceed {maxLength} characters.",
                       ImportText.Args(("max", maxLength)));
            return null;
        }
        return value;
    }

    public string? Code(ImportColumn column, int maxLength = 20)
    {
        var value = Text(column, maxLength);
        if (value is not null && !ImportText.IsCode(value))
        {
            errors.Add(row, column, ImportErrorCodes.InvalidCode,
                       $"{column.Header} can only use letters, digits and . _ / - (no spaces).");
            return null;
        }
        return value?.ToUpperInvariant();
    }

    public string? PersonName(ImportColumn column, int maxLength = 100)
    {
        var value = Text(column, maxLength);
        if (value is not null && !ImportText.IsName(value))
        {
            errors.Add(row, column, ImportErrorCodes.InvalidName, $"{column.Header} can only contain letters, spaces, apostrophes, dots and hyphens.");
            return null;
        }
        return value;
    }

    /// <summary>Email hamesha trim + lower-case mein save hota hai (login aur user link bhi isi se match karte hain).</summary>
    public string? Email(ImportColumn column)
    {
        var value = Text(column, 256);
        if (value is not null && !ImportText.IsEmail(value))
        {
            errors.Add(row, column, ImportErrorCodes.InvalidEmail, $"{column.Header} is not a valid email address.");
            return null;
        }
        return value?.ToLowerInvariant();
    }

    public string? Phone(ImportColumn column)
    {
        var value = Text(column, 50);
        if (value is not null && !ImportText.IsPhone(value))
        {
            errors.Add(row, column, ImportErrorCodes.InvalidPhone, $"{column.Header} must be a phone number like +92 300 1234567.");
            return null;
        }
        return value;
    }

    public DateOnly? Date(ImportColumn column)
    {
        var value = Text(column, 40);
        if (value is null)
            return null;
        if (!ImportText.TryDate(value, out var date))
        {
            errors.Add(row, column, ImportErrorCodes.InvalidDate, $"{column.Header} must be a date like 2026-01-31.");
            return null;
        }
        return date;
    }

    public T? Choice<T>(ImportColumn column, IReadOnlyDictionary<string, T> options, string optionList) where T : struct
    {
        var value = Text(column, 50);
        if (value is null)
            return null;
        if (!options.TryGetValue(ImportText.Key(value)!.Replace(" ", "").Replace("-", "").Replace("_", ""), out var choice))
        {
            errors.Add(row, column, ImportErrorCodes.InvalidChoice, $"{column.Header} must be one of: {optionList}.",
                       ImportText.Args(("options", optionList)));
            return null;
        }
        return choice;
    }

    public bool YesNo(ImportColumn column)
    {
        var value = Text(column, 10);
        if (value is null)
            return false;
        if (ImportText.TryYesNo(value) is { } yes)
            return yes;

        errors.Add(row, column, ImportErrorCodes.InvalidChoice, $"{column.Header} must be Yes or No.", ImportText.Args(("options", "Yes, No")));
        return false;
    }

    public void Fail(ImportColumn column, string code, string message, IReadOnlyDictionary<string, string>? args = null)
        => errors.Add(row, column, code, message, args);
}
