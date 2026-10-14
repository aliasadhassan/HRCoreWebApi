namespace HR.Employee.API.Application.Imports;

/// <summary>
/// Ek column ki uniqueness: file ke andar (pehli row yaad rehti hai) aur DB ke maujooda records ke saath.
/// Dono taraf chabi ImportText.Key (trim + lower) — "ALI@x.com " aur "ali@x.com" duplicate hain.
/// </summary>
public sealed class UniqueKeys(string label, IEnumerable<string?> existing)
{
    private readonly HashSet<string> _existing = existing.Select(ImportText.Key).OfType<string>().ToHashSet();
    private readonly Dictionary<string, int> _seen = [];

    public bool ExistsInSystem(string? value) => ImportText.Key(value) is { } key && _existing.Contains(key);

    /// <summary>Value theek hai to true. Duplicate ho to error likh kar false.</summary>
    public bool Claim(ImportCells cells, ImportColumn column, string? value)
    {
        if (ImportText.Key(value) is not { } key)
            return true;

        if (_existing.Contains(key))
        {
            cells.Fail(column, ImportErrorCodes.AlreadyExists, $"{label} '{value}' already exists in the system.");
            return false;
        }
        if (_seen.TryGetValue(key, out var firstRow))
        {
            cells.Fail(column, ImportErrorCodes.DuplicateInFile, $"{label} '{value}' is repeated in this file (first in row {firstRow}).",
                       ImportText.Args(("row", firstRow)));
            return false;
        }

        _seen[key] = cells.Row.Number;
        return true;
    }
}

/// <summary>
/// File ke andar parent/manager references ka loop (A → B → A). Har node ka ek parent; jo row loop mein ho uski list.
/// </summary>
public static class ImportGraph
{
    public static HashSet<int> RowsInLoops(IReadOnlyDictionary<int, int?> parentOf)
    {
        var inLoop = new HashSet<int>();
        var done = new HashSet<int>();
        foreach (var start in parentOf.Keys)
        {
            var path = new List<int>();
            var onPath = new HashSet<int>();
            int? current = start;
            while (current is { } node && !done.Contains(node))
            {
                if (!onPath.Add(node))
                {
                    // node se path ke aakhir tak loop hai
                    inLoop.UnionWith(path.SkipWhile(n => n != node));
                    break;
                }
                path.Add(node);
                current = parentOf.TryGetValue(node, out var parent) ? parent : null;
            }
            done.UnionWith(path);
        }
        return inLoop;
    }

    /// <summary>Jis row ka parent/manager (isi file mein) ghalat nikla, woh bhi nahi ban sakti — chain ke aakhir tak.</summary>
    public static void DropOrphans<T>(Dictionary<int, T> drafts, Func<T, int?> parentRow, Action<T, int> report)
    {
        bool removed;
        do
        {
            removed = false;
            foreach (var (row, d) in drafts.ToList())
            {
                if (parentRow(d) is { } p && !drafts.ContainsKey(p))
                {
                    report(d, p);
                    drafts.Remove(row);
                    removed = true;
                }
            }
        } while (removed);
    }

    /// <summary>Parents pehle (Id chahiye) — topological order. Loop wali rows pehle hi nikal di gayi hon.</summary>
    public static List<int> ParentsFirst(IReadOnlyDictionary<int, int?> parentOf)
    {
        var order = new List<int>(parentOf.Count);
        var placed = new HashSet<int>();
        foreach (var start in parentOf.Keys)
        {
            var chain = new Stack<int>();
            int? current = start;
            while (current is { } node && !placed.Contains(node) && parentOf.ContainsKey(node) && !chain.Contains(node))
            {
                chain.Push(node);
                current = parentOf[node];
            }
            while (chain.Count > 0)
            {
                var node = chain.Pop();
                if (placed.Add(node))
                    order.Add(node);
            }
        }
        return order;
    }
}
