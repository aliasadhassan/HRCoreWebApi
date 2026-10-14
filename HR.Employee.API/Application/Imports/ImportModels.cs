namespace HR.Employee.API.Application.Imports;

using System.Text.Json.Serialization;

/// <summary>Kaunsi sheet import ho rahi hai. Order bhi yahi: pehle locations, phir departments, designations, aakhir mein employees.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportKind { Locations = 1, Departments = 2, Designations = 3, Employees = 4 }

/// <summary>Template ka ek column. Header Excel mein English hi rehta hai; Key se match hota hai (case/space/* ignore).</summary>
public sealed record ImportColumn(string Key, string Header, bool Required, string Format, string Example, double Width = 18);

/// <summary>
/// Ek ghalti: kaunsi row, kaunsa column, kya value thi. Code UI translate karti hai (import.errors.*),
/// Message English fallback hai. Args (max, row, options) message mein bharne ke liye.
/// </summary>
public sealed record ImportError(
    int Row, string? Column, string? Value, string Code, string Message, IReadOnlyDictionary<string, string>? Args = null);

/// <summary>
/// Upload ka nateeja. Succeeded=false ho to DB mein kuch NAHI gaya (all-or-nothing).
/// Errors zyada hon to pehle MaxErrors hi bheje jate hain, ErrorCount poora hota hai.
/// </summary>
public sealed record ImportResult(
    ImportKind Kind, bool DryRun, bool Succeeded, int TotalRows, int Imported, int ErrorCount, IReadOnlyList<ImportError> Errors)
{
    public const int MaxErrors = 1000;
}

/// <summary>Excel ki ek data row. Number = Excel row number (header 1 hai, pehli data row 2).</summary>
public sealed class ImportRow(int number, IReadOnlyDictionary<string, string?> values)
{
    public int Number { get; } = number;

    /// <summary>Trim ki hui value; khali cell = null.</summary>
    public string? this[string key] => values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}

/// <summary>Validation ke dauran ghaltiyan jama karta hai. Row ki ek ghalti ke baad baqi columns bhi check hote hain — user ek hi baar sab theek kare.</summary>
public sealed class ImportErrors
{
    private readonly List<ImportError> _items = [];
    private readonly HashSet<int> _badRows = [];

    public int Count => _items.Count;
    public IReadOnlyList<ImportError> Items => _items;

    public bool RowHasErrors(int row) => _badRows.Contains(row);

    public void Add(int row, string? column, string? value, string code, string message, IReadOnlyDictionary<string, string>? args = null)
    {
        _badRows.Add(row);
        _items.Add(new ImportError(row, column, value is { Length: > 200 } ? value[..200] + "…" : value, code, message, args));
    }

    public void Add(ImportRow row, ImportColumn column, string code, string message, IReadOnlyDictionary<string, string>? args = null)
        => Add(row.Number, column.Header, row[column.Key], code, message, args);
}

/// <summary>Error codes — Angular i18n keys import.errors.&lt;code&gt; inhi naamon se hain.</summary>
public static class ImportErrorCodes
{
    public const string BadFile = "badFile";
    public const string EmptyFile = "emptyFile";
    public const string TooManyRows = "tooManyRows";
    public const string MissingColumn = "missingColumn";
    public const string Required = "required";
    public const string TooLong = "tooLong";
    public const string InvalidCode = "invalidCode";
    public const string InvalidName = "invalidName";
    public const string InvalidEmail = "invalidEmail";
    public const string InvalidPhone = "invalidPhone";
    public const string InvalidDate = "invalidDate";
    public const string InvalidNumber = "invalidNumber";
    public const string InvalidChoice = "invalidChoice";
    public const string InvalidCountry = "invalidCountry";
    public const string InvalidTimeZone = "invalidTimeZone";
    public const string InvalidWorkWeek = "invalidWorkWeek";
    public const string DuplicateInFile = "duplicateInFile";
    public const string AlreadyExists = "alreadyExists";
    public const string DuplicatePerson = "duplicatePerson";
    public const string NotFound = "notFound";
    public const string Inactive = "inactive";
    public const string SelfReference = "selfReference";
    public const string Loop = "loop";
    public const string DateOrder = "dateOrder";
    public const string DateRange = "dateRange";
    public const string AgeRange = "ageRange";
    public const string MultipleHeadOffices = "multipleHeadOffices";
    public const string RowInvalid = "rowInvalid";
}
