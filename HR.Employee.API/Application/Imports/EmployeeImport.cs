namespace HR.Employee.API.Application.Imports;

using HR.Employee.API.Domain.Employees;

/// <summary>
/// Employees ki sheet. Sab se zaroori checks:
///   • work email aur employee code duplicate na hon (file ke andar + system mein), trim + lower-case par
///   • ek hi shakhs dobara na aaye (naam + date of birth same)
///   • department / designation / location maujood aur active hon
///   • manager (misaal: Ali ka manager Moeen) isi file mein ho ya system mein, khud apna manager na ho, loop na bane
/// </summary>
public static class EmployeeImport
{
    public static readonly ImportColumn EmployeeCode = new("employeecode", "Employee code", false, "Up to 20 letters/digits, unique. Blank = next free EMP-0000 number.", "EMP-0102", 15);
    public static readonly ImportColumn FirstName = new("firstname", "First name", true, "Letters only, up to 100.", "Ali", 16);
    public static readonly ImportColumn MiddleName = new("middlename", "Middle name", false, "Letters only, up to 100.", "Raza", 14);
    public static readonly ImportColumn LastName = new("lastname", "Last name", true, "Letters only, up to 100.", "Hassan", 16);
    public static readonly ImportColumn WorkEmail = new("workemail", "Work email", true, "Unique. Saved in lower case; the user account links by this email.", "ali.hassan@company.com", 30);
    public static readonly ImportColumn PersonalEmail = new("personalemail", "Personal email", false, "Optional email.", "ali@gmail.com", 26);
    public static readonly ImportColumn Mobile = new("mobile", "Mobile", false, "Digits with optional +, spaces or dashes.", "+92 300 1234567", 18);
    public static readonly ImportColumn Gender = new("gender", "Gender", false, "Male, Female or Other.", "Male", 10);
    public static readonly ImportColumn DateOfBirth = new("dateofbirth", "Date of birth", false, "Date (YYYY-MM-DD). Age at joining 14 to 80.", "1995-04-18", 14);
    public static readonly ImportColumn Department = new("department", "Department", true, "Department code or name (must exist and be active).", "FIN", 18);
    public static readonly ImportColumn Designation = new("designation", "Designation", true, "Designation title (must exist and be active).", "Senior Accountant", 24);
    public static readonly ImportColumn Location = new("location", "Location", true, "Location code or name (must exist and be active).", "KHI-HO", 16);
    public static readonly ImportColumn Manager = new("manager", "Manager", false, "Employee code or work email of the manager: someone in this file or already in the system.", "moeen@company.com", 28);
    public static readonly ImportColumn EmploymentType = new("employmenttype", "Employment type", true, "Full time, Part time, Contract or Intern.", "Full time", 16);
    public static readonly ImportColumn JoiningDate = new("joiningdate", "Joining date", true, "Date (YYYY-MM-DD). Not more than a year ahead.", "2026-01-05", 14);
    public static readonly ImportColumn ProbationEndDate = new("probationenddate", "Probation end date", false, "Date after the joining date, within 2 years. Blank = no probation.", "2026-04-05", 18);

    public static readonly IReadOnlyList<ImportColumn> Columns =
    [
        EmployeeCode, FirstName, MiddleName, LastName, WorkEmail, PersonalEmail, Mobile, Gender, DateOfBirth,
        Department, Designation, Location, Manager, EmploymentType, JoiningDate, ProbationEndDate
    ];

    public static readonly IReadOnlyDictionary<string, EmploymentType> EmploymentTypes = new Dictionary<string, EmploymentType>
    {
        ["fulltime"] = Domain.Employees.EmploymentType.FullTime,
        ["permanent"] = Domain.Employees.EmploymentType.FullTime,
        ["parttime"] = Domain.Employees.EmploymentType.PartTime,
        ["contract"] = Domain.Employees.EmploymentType.Contract,
        ["contractor"] = Domain.Employees.EmploymentType.Contract,
        ["intern"] = Domain.Employees.EmploymentType.Intern,
        ["internship"] = Domain.Employees.EmploymentType.Intern
    };

    public static readonly IReadOnlyDictionary<string, Gender> Genders = new Dictionary<string, Gender>
    {
        ["male"] = Domain.Employees.Gender.Male,
        ["m"] = Domain.Employees.Gender.Male,
        ["female"] = Domain.Employees.Gender.Female,
        ["f"] = Domain.Employees.Gender.Female,
        ["other"] = Domain.Employees.Gender.Other
    };

    public const string EmploymentTypeList = "Full time, Part time, Contract, Intern";
    public const string GenderList = "Male, Female, Other";

    public sealed record OrgRef(Guid Id, string? Code, string Name, bool IsActive);

    public sealed record ExistingEmployee(
        Guid Id, string Code, string Email, string FirstName, string LastName, DateOnly? DateOfBirth, bool Exited);

    public sealed record Snapshot(
        IReadOnlyCollection<ExistingEmployee> Employees,
        IReadOnlyCollection<string> AllCodesIncludingDeleted,
        int CountIncludingDeleted,
        IReadOnlyCollection<OrgRef> Departments,
        IReadOnlyCollection<OrgRef> Designations,
        IReadOnlyCollection<OrgRef> Locations,
        DateOnly Today);

    public sealed record Planned(
        int Row, string Code, string FirstName, string? MiddleName, string LastName, string WorkEmail,
        string? PersonalEmail, string? Mobile, Gender? Gender, DateOnly? DateOfBirth,
        Guid DepartmentId, Guid DesignationId, Guid LocationId, int? ManagerRow, Guid? ManagerId,
        EmploymentType EmploymentType, DateOnly JoiningDate, DateOnly? ProbationEndDate);

    private sealed record Draft(
        int Row, string? Code, string FirstName, string? MiddleName, string LastName, string WorkEmail,
        string? PersonalEmail, string? Mobile, Gender? Gender, DateOnly? DateOfBirth,
        Guid DepartmentId, Guid DesignationId, Guid LocationId, int? ManagerRow, Guid? ManagerId,
        EmploymentType EmploymentType, DateOnly JoiningDate, DateOnly? ProbationEndDate);

    /// <summary>Managers pehle (Id chahiye) order mein plan. Ek bhi error ho to handler kuch save nahi karta.</summary>
    public static List<Planned> Plan(IReadOnlyList<ImportRow> rows, Snapshot existing, ImportErrors errors)
    {
        var emails = new UniqueKeys("Work email", existing.Employees.Select(e => e.Email));
        var codes = new UniqueKeys("Employee code", existing.Employees.Select(e => e.Code));
        var people = new EmployeeLookup(existing.Employees.Select(e => new EmployeeLookup.Person(e.Id, e.Code, e.Email, e.Exited)));

        // Same naam + date of birth = wahi shakhs (dusri email ke saath dobara)
        var personInSystem = new Dictionary<string, string>();
        foreach (var e in existing.Employees.Where(e => e.DateOfBirth is not null))
            personInSystem.TryAdd(PersonKey(e.FirstName, e.LastName, e.DateOfBirth), e.Code);
        var personInFile = new Dictionary<string, int>();

        var departments = new OrgIndex("Department", existing.Departments);
        var designations = new OrgIndex("Designation", existing.Designations);
        var locations = new OrgIndex("Location", existing.Locations);

        // Manager file ki kisi row ka ho sakta hai: code ya email se
        var fileRowByKey = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            if (ImportText.Key(row[EmployeeCode.Key]) is { } code)
                fileRowByKey.TryAdd(code, row.Number);
            if (ImportText.Key(row[WorkEmail.Key]) is { } email)
                fileRowByKey.TryAdd(email, row.Number);
        }

        var latestJoining = existing.Today.AddYears(1);
        var earliestJoining = new DateOnly(1950, 1, 1);
        var rowByNumber = rows.ToDictionary(r => r.Number);
        var drafts = new Dictionary<int, Draft>();

        foreach (var row in rows)
        {
            var c = new ImportCells(row, errors);

            var code = c.Code(EmployeeCode);
            codes.Claim(c, EmployeeCode, code);
            var first = c.PersonName(FirstName);
            var middle = c.PersonName(MiddleName);
            var last = c.PersonName(LastName);
            var workEmail = c.Email(WorkEmail);
            emails.Claim(c, WorkEmail, workEmail);
            var personalEmail = c.Email(PersonalEmail);
            var mobile = c.Phone(Mobile);
            var gender = c.Choice(Gender, Genders, GenderList);
            var dob = c.Date(DateOfBirth);

            var departmentId = departments.Resolve(c, Department, c.Text(Department, 150));
            var designationId = designations.Resolve(c, Designation, c.Text(Designation, 150));
            var locationId = locations.Resolve(c, Location, c.Text(Location, 150));
            var type = c.Choice(EmploymentType, EmploymentTypes, EmploymentTypeList);

            var joining = c.Date(JoiningDate);
            if (joining is { } j && (j < earliestJoining || j > latestJoining))
            {
                c.Fail(JoiningDate, ImportErrorCodes.DateRange, $"Joining date must be between {earliestJoining:yyyy-MM-dd} and {latestJoining:yyyy-MM-dd}.",
                       ImportText.Args(("from", earliestJoining.ToString("yyyy-MM-dd")), ("to", latestJoining.ToString("yyyy-MM-dd"))));
                joining = null;
            }

            var probation = c.Date(ProbationEndDate);
            if (probation is { } p && joining is { } jd && (p <= jd || p > jd.AddYears(2)))
                c.Fail(ProbationEndDate, ImportErrorCodes.DateOrder, "Probation end date must be after the joining date and within 2 years of it.");

            if (dob is { } birth && joining is { } joined)
            {
                var age = AgeOn(birth, joined);
                if (birth >= joined)
                    c.Fail(DateOfBirth, ImportErrorCodes.DateOrder, "Date of birth must be before the joining date.");
                else if (age is < 14 or > 80)
                    c.Fail(DateOfBirth, ImportErrorCodes.AgeRange, $"Age at joining would be {age}; it must be between 14 and 80.",
                           ImportText.Args(("age", age), ("min", 14), ("max", 80)));
            }

            if (first is not null && last is not null && dob is not null)
            {
                var person = PersonKey(first, last, dob);
                if (personInSystem.TryGetValue(person, out var existingCode))
                    c.Fail(DateOfBirth, ImportErrorCodes.DuplicatePerson,
                           $"{first} {last} with this date of birth is already in the system ({existingCode}).", ImportText.Args(("code", existingCode)));
                else if (personInFile.TryGetValue(person, out var firstRow))
                    c.Fail(DateOfBirth, ImportErrorCodes.DuplicatePerson,
                           $"{first} {last} with this date of birth is repeated in this file (first in row {firstRow}).", ImportText.Args(("row", firstRow)));
                else
                    personInFile[person] = row.Number;
            }

            int? managerRow = null;
            Guid? managerId = null;
            if (c.Text(Manager, 256) is { } manager)
            {
                var key = ImportText.Key(manager)!;
                if (key == ImportText.Key(code) || key == ImportText.Key(workEmail))
                    c.Fail(Manager, ImportErrorCodes.SelfReference, "An employee cannot be their own manager.");
                else if (people.Find(manager) is not null || !fileRowByKey.TryGetValue(key, out var r))
                    managerId = people.Resolve(c, Manager, manager);   // system wala pehle (exit hua ho to error)
                else
                    managerRow = r;
            }

            if (!errors.RowHasErrors(row.Number))
                drafts[row.Number] = new Draft(row.Number, code, first!, middle, last!, workEmail!, personalEmail, mobile, gender, dob,
                                               departmentId!.Value, designationId!.Value, locationId!.Value, managerRow, managerId,
                                               type!.Value, joining!.Value, probation);
        }

        foreach (var row in ImportGraph.RowsInLoops(drafts.Values.ToDictionary(d => d.Row, d => d.ManagerRow)))
        {
            errors.Add(row, Manager.Header, rowByNumber[row][Manager.Key], ImportErrorCodes.Loop, "These managers form a reporting loop (A → B → A).");
            drafts.Remove(row);
        }

        ImportGraph.DropOrphans(drafts, d => d.ManagerRow, (d, managerRow) =>
            errors.Add(d.Row, Manager.Header, rowByNumber[d.Row][Manager.Key], ImportErrorCodes.RowInvalid, $"The manager's row ({managerRow}) has errors.",
                       ImportText.Args(("row", managerRow))));

        // Code khali → agla khali EMP-0000 (soft-deleted ke codes bhi skip, purana code dobara na mile)
        var used = existing.AllCodesIncludingDeleted.Select(x => x.ToUpperInvariant()).ToHashSet();
        foreach (var r in rows)
            if (r[EmployeeCode.Key] is { } given)
                used.Add(given.Trim().ToUpperInvariant());
        var next = existing.CountIncludingDeleted + 1;

        var ordered = ImportGraph.ParentsFirst(drafts.Values.ToDictionary(d => d.Row, d => d.ManagerRow));
        var plan = new List<Planned>(ordered.Count);
        foreach (var d in ordered.Select(r => drafts[r]).OrderBy(d => d.Row).ToList())
        {
            var code = d.Code;
            while (code is null)
            {
                var candidate = $"EMP-{next++:D4}";
                if (used.Add(candidate))
                    code = candidate;
            }
            drafts[d.Row] = d with { Code = code };
        }

        foreach (var r in ordered)
        {
            var d = drafts[r];
            plan.Add(new Planned(d.Row, d.Code!, d.FirstName, d.MiddleName, d.LastName, d.WorkEmail, d.PersonalEmail, d.Mobile, d.Gender,
                                 d.DateOfBirth, d.DepartmentId, d.DesignationId, d.LocationId, d.ManagerRow, d.ManagerId,
                                 d.EmploymentType, d.JoiningDate, d.ProbationEndDate));
        }
        return plan;
    }

    private static string PersonKey(string first, string last, DateOnly? dob)
        => $"{ImportText.Key(first)}|{ImportText.Key(last)}|{dob:yyyy-MM-dd}";

    private static int AgeOn(DateOnly birth, DateOnly on)
    {
        var age = on.Year - birth.Year;
        return birth.AddYears(age) > on ? age - 1 : age;
    }

    /// <summary>Department/location: code ya naam; designation: title. Ghayab ya inactive = error.</summary>
    private sealed class OrgIndex
    {
        private readonly string _label;
        private readonly Dictionary<string, OrgRef> _byKey = [];

        public OrgIndex(string label, IEnumerable<OrgRef> items)
        {
            _label = label;
            var list = items.ToList();
            foreach (var item in list.Where(i => i.Code is not null))
                _byKey.TryAdd(ImportText.Key(item.Code)!, item);
            foreach (var item in list)
                _byKey.TryAdd(ImportText.Key(item.Name)!, item);
        }

        public Guid? Resolve(ImportCells cells, ImportColumn column, string? value)
        {
            if (value is null)
                return null;   // Required wali ghalti Text() pehle hi likh chuka
            if (!_byKey.TryGetValue(ImportText.Key(value)!, out var item))
            {
                cells.Fail(column, ImportErrorCodes.NotFound, $"{_label} '{value}' was not found. Add it first (Organization page or its import).");
                return null;
            }
            if (!item.IsActive)
            {
                cells.Fail(column, ImportErrorCodes.Inactive, $"{_label} '{value}' is inactive.");
                return null;
            }
            return item.Id;
        }
    }
}
