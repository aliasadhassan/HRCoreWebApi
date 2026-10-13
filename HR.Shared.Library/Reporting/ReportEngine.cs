namespace HR.Shared.Library.Reporting;

using System.Globalization;

/// <summary>Report builder ke field ki qisam — filter ka tareeqa aur column ki formatting isi se.</summary>
public enum ReportFieldType { Text, Number, Money, Date, Bool }

/// <summary>NoTotal = number hai magar jor ka matlab nahi (umar, level) — bina group wale report ke total row mein khaali.</summary>
public sealed record ReportFieldDef(string Key, string Label, ReportFieldType Type, string Group, bool NoTotal = false);

/// <summary>
/// Ek data source (Employees, Attendance, Payslips…). Key service ke andar unique; client "employee:attendance" /
/// "payroll:payslips" jaisa prefix lagata hai. NeedsRange = from/to zaroori (bari tables).
/// </summary>
public sealed record ReportSourceDef(string Key, string Label, string Description, bool NeedsRange, IReadOnlyList<ReportFieldDef> Fields);

public sealed record ReportFilter(string Field, string Op, string? Value, string? Value2 = null);

/// <summary>Fn: count, sum, avg, min, max. count ke liye Field khaali ho sakta hai (rows ginna).</summary>
public sealed record ReportAggregate(string Fn, string? Field);

public sealed record ReportSort(string Key, bool Desc);

/// <summary>Saved report ki asal shakal (jsonb mein yahi). Columns = bina group ke dikhne wale fields.</summary>
public sealed class ReportSpec
{
    public List<string> Columns { get; set; } = [];
    public List<ReportFilter> Filters { get; set; } = [];
    public string? GroupBy { get; set; }
    public List<ReportAggregate> Aggregates { get; set; } = [];
    public ReportSort? Sort { get; set; }
    public int? Limit { get; set; }
}

public sealed record ReportColumn(string Key, string Label, ReportFieldType Type);

public sealed record ReportResult(
    IReadOnlyList<ReportColumn> Columns, IReadOnlyList<object?[]> Rows, object?[]? Totals, int TotalRows, bool Truncated);

/// <summary>Spec mein ghalti (anjaan field, ghalat operator, bura value) — API 400 deti hai.</summary>
public sealed class ReportSpecException(string message) : Exception(message);

/// <summary>
/// Saved report chalane wala engine: memory mein filter → group/aggregate → sort → limit.
/// Rows source loader se aati hain (tenant + RLS wahan); yahan sirf whitelisted fields, koi SQL nahi.
/// </summary>
public static class ReportEngine
{
    public const int MaxColumns = 30;
    public const int MaxFilters = 12;
    public const int MaxAggregates = 6;
    public const int MaxRows = 5000;

    public static readonly string[] Ops = ["eq", "ne", "contains", "starts", "gt", "gte", "lt", "lte", "between", "empty", "notempty", "in"];
    public static readonly string[] Fns = ["count", "sum", "avg", "min", "max"];

    /// <summary>Spec ko source ke against jaanch — save aur run dono se pehle.</summary>
    public static void Validate(ReportSourceDef source, ReportSpec spec)
    {
        var fields = source.Fields.ToDictionary(f => f.Key);
        ReportFieldDef Field(string? key, string what)
            => key is not null && fields.TryGetValue(key, out var f) ? f : throw new ReportSpecException($"Unknown {what} field '{key}' for {source.Label}.");

        if (spec.GroupBy is null && spec.Columns.Count == 0)
            throw new ReportSpecException("Pick at least one column.");
        if (spec.Columns.Count > MaxColumns)
            throw new ReportSpecException($"A report can have at most {MaxColumns} columns.");
        if (spec.Columns.Distinct().Count() != spec.Columns.Count)
            throw new ReportSpecException("A column is listed twice.");
        foreach (var c in spec.Columns)
            Field(c, "column");

        if (spec.Filters.Count > MaxFilters)
            throw new ReportSpecException($"A report can have at most {MaxFilters} filters.");
        foreach (var f in spec.Filters)
        {
            var def = Field(f.Field, "filter");
            if (!Ops.Contains(f.Op))
                throw new ReportSpecException($"Unknown filter operator '{f.Op}'.");
            if (f.Op is "empty" or "notempty")
                continue;
            if ((f.Op is "contains" or "starts") && def.Type != ReportFieldType.Text)
                throw new ReportSpecException($"'{def.Label}' is not text, so it cannot use '{f.Op}'.");
            if ((f.Op is "gt" or "gte" or "lt" or "lte" or "between") && def.Type is ReportFieldType.Text or ReportFieldType.Bool)
                throw new ReportSpecException($"'{def.Label}' cannot be compared with '{f.Op}'.");
            if (string.IsNullOrWhiteSpace(f.Value) || (f.Op == "between" && string.IsNullOrWhiteSpace(f.Value2)))
                throw new ReportSpecException($"Filter on '{def.Label}' needs a value.");
            foreach (var v in FilterValues(f))
                if (Parse(def.Type, v) is null)
                    throw new ReportSpecException($"'{v}' is not a valid value for '{def.Label}'.");
        }

        if (spec.GroupBy is not null)
        {
            Field(spec.GroupBy, "group");
            if (spec.Aggregates.Count is 0 or > MaxAggregates)
                throw new ReportSpecException($"A grouped report needs 1 to {MaxAggregates} totals.");
            foreach (var a in spec.Aggregates)
            {
                if (!Fns.Contains(a.Fn))
                    throw new ReportSpecException($"Unknown total '{a.Fn}'.");
                if (a.Fn == "count" && string.IsNullOrEmpty(a.Field))
                    continue;
                var def = Field(a.Field, "total");
                if (a.Fn is "sum" or "avg" && def.Type is not (ReportFieldType.Number or ReportFieldType.Money))
                    throw new ReportSpecException($"'{def.Label}' is not a number, so it cannot be summed or averaged.");
                if (a.Fn is "min" or "max" && def.Type is ReportFieldType.Text or ReportFieldType.Bool)
                    throw new ReportSpecException($"'{def.Label}' has no min or max.");
            }
            if (spec.Aggregates.Select(AggKey).Distinct().Count() != spec.Aggregates.Count)
                throw new ReportSpecException("A total is listed twice.");
        }
        else if (spec.Aggregates.Count > 0)
            throw new ReportSpecException("Totals need a group-by field.");

        if (spec.Sort is { } s && !OutputColumns(source, spec).Any(c => c.Key == s.Key))
            throw new ReportSpecException($"Cannot sort by '{s.Key}': it is not a column of this report.");
        if (spec.Limit is < 1 or > MaxRows)
            throw new ReportSpecException($"Row limit must be between 1 and {MaxRows}.");
    }

    public static string AggKey(ReportAggregate a) => string.IsNullOrEmpty(a.Field) ? a.Fn : $"{a.Fn}:{a.Field}";

    public static IReadOnlyList<ReportColumn> OutputColumns(ReportSourceDef source, ReportSpec spec)
    {
        var fields = source.Fields.ToDictionary(f => f.Key);
        if (spec.GroupBy is null)
            return spec.Columns.Select(c => new ReportColumn(c, fields[c].Label, fields[c].Type)).ToList();

        var cols = new List<ReportColumn> { new(spec.GroupBy, fields[spec.GroupBy].Label, fields[spec.GroupBy].Type) };
        foreach (var a in spec.Aggregates)
        {
            if (string.IsNullOrEmpty(a.Field))
            {
                cols.Add(new(AggKey(a), "Count", ReportFieldType.Number));
                continue;
            }
            var f = fields[a.Field];
            var type = a.Fn switch
            {
                "count" => ReportFieldType.Number,
                "avg" => f.Type == ReportFieldType.Money ? ReportFieldType.Money : ReportFieldType.Number,
                _ => f.Type,
            };
            var label = a.Fn switch
            {
                "count" => $"Count of {f.Label}",
                "sum" => $"Total {f.Label}",
                "avg" => $"Average {f.Label}",
                "min" => $"Lowest {f.Label}",
                _ => $"Highest {f.Label}",
            };
            cols.Add(new(AggKey(a), label, type));
        }
        return cols;
    }

    public static ReportResult Run(ReportSourceDef source, ReportSpec spec, IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        Validate(source, spec);
        var fields = source.Fields.ToDictionary(f => f.Key);
        var filtered = rows.Where(r => spec.Filters.All(f => Matches(fields[f.Field].Type, Get(r, f.Field), f))).ToList();
        var columns = OutputColumns(source, spec);

        List<object?[]> output;
        object?[]? totals;
        if (spec.GroupBy is { } g)
        {
            var groups = filtered.GroupBy(r => Key(Get(r, g)));
            output = groups.Select(grp =>
            {
                var row = new object?[columns.Count];
                row[0] = Out(fields[g].Type, Get(grp.First(), g));
                for (var i = 0; i < spec.Aggregates.Count; i++)
                    row[i + 1] = Aggregate(spec.Aggregates[i], grp, fields);
                return row;
            }).ToList();
            totals = new object?[columns.Count];
            totals[0] = null;
            for (var i = 0; i < spec.Aggregates.Count; i++)
                totals[i + 1] = Aggregate(spec.Aggregates[i], filtered, fields);
        }
        else
        {
            output = filtered.Select(r => spec.Columns.Select(c => Out(fields[c].Type, Get(r, c))).ToArray()).ToList();
            bool Summable(ReportColumn c) => c.Type is ReportFieldType.Money or ReportFieldType.Number && !fields[c.Key].NoTotal;
            totals = columns.Any(Summable)
                ? columns.Select((c, i) => Summable(c) ? (object?)output.Sum(r => r[i] is decimal d ? d : 0m) : null).ToArray()
                : null;
        }

        if (spec.Sort is { } s)
        {
            var idx = columns.ToList().FindIndex(c => c.Key == s.Key);
            var ordered = s.Desc
                ? output.OrderByDescending(r => r[idx], ValueComparer.Instance)
                : output.OrderBy(r => r[idx], ValueComparer.Instance);
            output = ordered.ToList();
        }
        else if (spec.GroupBy is not null)
            output = output.OrderBy(r => r[0], ValueComparer.Instance).ToList();

        var total = output.Count;
        var limit = Math.Min(spec.Limit ?? MaxRows, MaxRows);
        return new ReportResult(columns, output.Take(limit).ToList(), totals, total, total > limit);
    }

    private static object? Get(IReadOnlyDictionary<string, object?> row, string key) => row.TryGetValue(key, out var v) ? v : null;

    private static string Key(object? v) => v switch
    {
        null => "",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };

    /// <summary>JSON ke liye: date "yyyy-MM-dd", number decimal, baqi jaisa.</summary>
    private static object? Out(ReportFieldType type, object? v) => v switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ when type is ReportFieldType.Number or ReportFieldType.Money => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
        _ => v,
    };

    private static object? Aggregate(ReportAggregate a, IEnumerable<IReadOnlyDictionary<string, object?>> rows, Dictionary<string, ReportFieldDef> fields)
    {
        if (string.IsNullOrEmpty(a.Field))
            return (decimal)rows.Count();
        var type = fields[a.Field].Type;
        var values = rows.Select(r => Get(r, a.Field)).Where(v => v is not null).ToList();
        switch (a.Fn)
        {
            case "count":
                return (decimal)values.Count;
            case "sum":
                return values.Sum(v => Convert.ToDecimal(v, CultureInfo.InvariantCulture));
            case "avg":
                return values.Count == 0 ? null : Math.Round(values.Average(v => Convert.ToDecimal(v, CultureInfo.InvariantCulture)), 2);
            default:
                if (values.Count == 0)
                    return null;
                var sorted = values.Select(v => Out(type, v)).OrderBy(v => v, ValueComparer.Instance).ToList();
                return a.Fn == "min" ? sorted[0] : sorted[^1];
        }
    }

    private static IEnumerable<string> FilterValues(ReportFilter f) => f.Op switch
    {
        "in" => (f.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        "between" => [f.Value!, f.Value2!],
        _ => [f.Value!],
    };

    /// <summary>Client ka string value field ki qisam mein. Ghalat ho to null.</summary>
    public static object? Parse(ReportFieldType type, string value) => type switch
    {
        ReportFieldType.Number or ReportFieldType.Money
            => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null,
        ReportFieldType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null,
        ReportFieldType.Bool => bool.TryParse(value, out var b) ? b : null,
        _ => value,
    };

    private static bool Matches(ReportFieldType type, object? v, ReportFilter f)
    {
        var empty = v is null || (v is string s0 && s0.Length == 0);
        if (f.Op == "empty") return empty;
        if (f.Op == "notempty") return !empty;
        if (empty) return f.Op == "ne";

        if (type == ReportFieldType.Text)
        {
            var text = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            var want = f.Value ?? "";
            return f.Op switch
            {
                "eq" => text.Equals(want, StringComparison.OrdinalIgnoreCase),
                "ne" => !text.Equals(want, StringComparison.OrdinalIgnoreCase),
                "contains" => text.Contains(want, StringComparison.OrdinalIgnoreCase),
                "starts" => text.StartsWith(want, StringComparison.OrdinalIgnoreCase),
                "in" => FilterValues(f).Any(x => text.Equals(x, StringComparison.OrdinalIgnoreCase)),
                _ => false,
            };
        }

        var cmp = (string raw) => ValueComparer.Instance.Compare(Normal(v), Normal(Parse(type, raw)));
        return f.Op switch
        {
            "eq" => cmp(f.Value!) == 0,
            "ne" => cmp(f.Value!) != 0,
            "gt" => cmp(f.Value!) > 0,
            "gte" => cmp(f.Value!) >= 0,
            "lt" => cmp(f.Value!) < 0,
            "lte" => cmp(f.Value!) <= 0,
            "between" => cmp(f.Value!) >= 0 && cmp(f.Value2!) <= 0,
            "in" => FilterValues(f).Any(x => cmp(x) == 0),
            _ => false,
        };
    }

    private static object? Normal(object? v) => v switch
    {
        null or DateOnly or bool or string => v,
        IConvertible => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
        _ => v,
    };

    /// <summary>null sab se pehle; number, date, bool, text apni apni qisam se; text case-insensitive.</summary>
    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x is null) return y is null ? 0 : -1;
            if (y is null) return 1;
            x = Normal(x);
            y = Normal(y);
            return (x, y) switch
            {
                (decimal a, decimal b) => a.CompareTo(b),
                (DateOnly a, DateOnly b) => a.CompareTo(b),
                (bool a, bool b) => a.CompareTo(b),
                _ => string.Compare(Convert.ToString(x, CultureInfo.InvariantCulture), Convert.ToString(y, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase),
            };
        }
    }
}
