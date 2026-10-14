namespace HR.Employee.API.Application.Imports;

/// <summary>Departments: parent department isi file mein ho sakta hai ya pehle se system mein; head ek maujooda employee.</summary>
public static class DepartmentImport
{
    public static readonly ImportColumn Code = new("code", "Code", true, "Up to 20 letters/digits, unique (saved in capitals).", "FIN", 12);
    public static readonly ImportColumn Name = new("name", "Name", true, "Up to 150 characters, unique.", "Finance", 26);
    public static readonly ImportColumn Description = new("description", "Description", false, "Up to 500 characters.", "Accounts, treasury and audit", 34);
    public static readonly ImportColumn Parent = new("parent", "Parent department", false, "Code of a department in this file or already in the system.", "CORP", 18);
    public static readonly ImportColumn Head = new("head", "Head", false, "Employee code or work email of an existing employee.", "EMP-0001", 26);

    public static readonly IReadOnlyList<ImportColumn> Columns = [Code, Name, Description, Parent, Head];

    public sealed record Existing(Guid Id, string Code, string Name);

    public sealed record Planned(int Row, string Code, string Name, string? Description, int? ParentRow, Guid? ParentId, Guid? HeadEmployeeId);

    /// <summary>Plan parents-pehle order mein lautata hai, taake child banate waqt parent ki Id mil chuki ho.</summary>
    public static List<Planned> Plan(
        IReadOnlyList<ImportRow> rows, IReadOnlyCollection<Existing> existing, EmployeeLookup employees, ImportErrors errors)
    {
        var codes = new UniqueKeys("Department code", existing.Select(x => x.Code));
        var names = new UniqueKeys("Department name", existing.Select(x => x.Name));
        var existingByCode = existing.ToDictionary(x => ImportText.Key(x.Code)!, x => x.Id);

        // Pehle saari codes, taake row 2 ka parent row 9 mein ho to bhi mil jaye
        var fileRowByCode = new Dictionary<string, int>();
        foreach (var row in rows)
            if (ImportText.Key(row[Code.Key]) is { } key)
                fileRowByCode.TryAdd(key, row.Number);

        var rowByNumber = rows.ToDictionary(r => r.Number);
        var drafts = new Dictionary<int, Planned>();
        foreach (var row in rows)
        {
            var c = new ImportCells(row, errors);
            var code = c.Code(Code);
            var name = c.Text(Name, 150);
            var description = c.Text(Description, 500);
            codes.Claim(c, Code, code);
            names.Claim(c, Name, name);

            int? parentRow = null;
            Guid? parentId = null;
            if (c.Code(Parent) is { } parentCode)
            {
                var key = ImportText.Key(parentCode)!;
                if (key == ImportText.Key(code))
                    c.Fail(Parent, ImportErrorCodes.SelfReference, "A department cannot be its own parent.");
                else if (existingByCode.TryGetValue(key, out var id))
                    parentId = id;   // system wala pehle: file mein same code ho to woh row khud duplicate error hai
                else if (fileRowByCode.TryGetValue(key, out var r))
                    parentRow = r;
                else
                    c.Fail(Parent, ImportErrorCodes.NotFound, $"Parent department '{parentCode}' was not found in this file or in the system.");
            }

            Guid? headId = null;
            if (c.Text(Head, 256) is { } head)
                headId = employees.Resolve(c, Head, head);

            if (!errors.RowHasErrors(row.Number))
                drafts[row.Number] = new Planned(row.Number, code!, name!, description, parentRow, parentId, headId);
        }

        // Parent ki row ghalat ho to child bhi nahi ban sakta
        var parentOf = drafts.Values.ToDictionary(d => d.Row, d => d.ParentRow);
        foreach (var row in ImportGraph.RowsInLoops(parentOf))
        {
            errors.Add(row, Parent.Header, rowByNumber[row][Parent.Key], ImportErrorCodes.Loop, "These parent departments form a loop (A → B → A).");
            drafts.Remove(row);
        }

        ImportGraph.DropOrphans(drafts, d => d.ParentRow, (d, parentRow) =>
            errors.Add(d.Row, Parent.Header, rowByNumber[d.Row][Parent.Key], ImportErrorCodes.RowInvalid, $"Parent department row {parentRow} has errors.",
                       ImportText.Args(("row", parentRow))));

        return ImportGraph.ParentsFirst(drafts.Values.ToDictionary(d => d.Row, d => d.ParentRow))
            .Select(r => drafts[r])
            .ToList();
    }
}

/// <summary>Maujooda employees ko code ya work email se dhoondna (department head, manager).</summary>
public sealed class EmployeeLookup
{
    public sealed record Person(Guid Id, string Code, string Email, bool Exited);

    private readonly Dictionary<string, Person> _byKey = [];

    public EmployeeLookup(IEnumerable<Person> people)
    {
        foreach (var p in people)
        {
            _byKey.TryAdd(ImportText.Key(p.Code)!, p);
            _byKey.TryAdd(ImportText.Key(p.Email)!, p);
        }
    }

    public Person? Find(string codeOrEmail) => _byKey.GetValueOrDefault(ImportText.Key(codeOrEmail)!);

    /// <summary>Mil jaye aur exit na hua ho to Id; warna error likh kar null.</summary>
    public Guid? Resolve(ImportCells cells, ImportColumn column, string codeOrEmail)
    {
        var person = Find(codeOrEmail);
        if (person is null)
        {
            cells.Fail(column, ImportErrorCodes.NotFound, $"No employee with code or email '{codeOrEmail}'.");
            return null;
        }
        if (person.Exited)
        {
            cells.Fail(column, ImportErrorCodes.Inactive, $"'{codeOrEmail}' has left the company.");
            return null;
        }
        return person.Id;
    }
}
