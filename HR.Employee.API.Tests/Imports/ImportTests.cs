namespace HR.Employee.API.Tests.Imports;

using ClosedXML.Excel;
using HR.Employee.API.Application.Imports;
using HR.Employee.API.Domain.Employees;
using Xunit;

public class ImportTests
{
    static readonly DateOnly Today = new(2026, 10, 14);

    static ImportRow Row(int number, params (string Key, string? Value)[] cells)
        => new(number, cells.ToDictionary(c => c.Key, c => c.Value));

    static readonly Guid Fin = Guid.NewGuid(), Hr = Guid.NewGuid(), Old = Guid.NewGuid();
    static readonly Guid Accountant = Guid.NewGuid(), Manager = Guid.NewGuid();
    static readonly Guid Khi = Guid.NewGuid();
    static readonly Guid Ceo = Guid.NewGuid(), Gone = Guid.NewGuid();

    static EmployeeImport.Snapshot Snapshot() => new(
        [
            new(Ceo, "EMP-0001", "ceo@hr-cloud.online", "Sara", "Khan", new DateOnly(1980, 1, 1), false),
            new(Gone, "EMP-0002", "gone@hr-cloud.online", "Bilal", "Ahmed", null, true)
        ],
        ["EMP-0001", "EMP-0002", "EMP-0003"],   // EMP-0003 soft-deleted
        3,
        [new(Fin, "FIN", "Finance", true), new(Hr, "HR", "Human Resources", true), new(Old, "OLD", "Old dept", false)],
        [new(Accountant, null, "Accountant", true), new(Manager, null, "Manager", true)],
        [new(Khi, "KHI", "Karachi", true)],
        Today);

    static ImportRow Person(int row, string first, string email, string? manager = null, string? code = null,
                            string dept = "FIN", string? dob = null, string type = "Full time")
        => Row(row,
            ("employeecode", code), ("firstname", first), ("lastname", "Test"), ("workemail", email),
            ("department", dept), ("designation", "Accountant"), ("location", "KHI"),
            ("manager", manager), ("employmenttype", type), ("joiningdate", "2026-01-05"), ("dateofbirth", dob));

    [Fact]
    public void Valid_file_is_planned_with_managers_first_and_auto_codes()
    {
        var errors = new ImportErrors();
        var plan = EmployeeImport.Plan(
        [
            Person(2, "Ali", "Ali@Company.com ", manager: "moeen@company.com"),   // Ali ka manager Moeen (neeche wali row)
            Person(3, "Moeen", "moeen@company.com", manager: "EMP-0001"),         // Moeen ka manager system mein
            Person(4, "Zara", "zara@company.com", code: "emp-0100")
        ], Snapshot(), errors);

        Assert.Equal(0, errors.Count);
        Assert.Equal([3, 2, 4], plan.Select(p => p.Row).ToArray());

        var ali = plan.Single(p => p.Row == 2);
        Assert.Equal("ali@company.com", ali.WorkEmail);   // trim + lower
        Assert.Equal(3, ali.ManagerRow);
        Assert.Equal("EMP-0004", ali.Code);               // 0003 soft-deleted hai, skip

        var moeen = plan.Single(p => p.Row == 3);
        Assert.Equal(Ceo, moeen.ManagerId);
        Assert.Equal("EMP-0005", moeen.Code);
        Assert.Equal("EMP-0100", plan.Single(p => p.Row == 4).Code);
    }

    [Fact]
    public void Duplicate_emails_are_caught_in_file_and_in_system_ignoring_case_and_spaces()
    {
        var errors = new ImportErrors();
        var plan = EmployeeImport.Plan(
        [
            Person(2, "Ali", "ali@company.com"),
            Person(3, "Ali", "  ALI@company.COM"),
            Person(4, "Sara", "CEO@hr-cloud.online")
        ], Snapshot(), errors);

        Assert.Contains(errors.Items, e => e.Row == 3 && e.Code == ImportErrorCodes.DuplicateInFile && e.Args!["row"] == "2");
        Assert.Contains(errors.Items, e => e.Row == 4 && e.Code == ImportErrorCodes.AlreadyExists);
        Assert.Single(plan);
    }

    [Fact]
    public void Same_person_with_another_email_is_rejected()
    {
        var errors = new ImportErrors();
        EmployeeImport.Plan([Person(2, "sara", "sara.k@company.com", dob: "1980-01-01")], Snapshot(), errors);

        // naam "sara Test" hai, system mein "Sara Khan" — alag shakhs
        Assert.Equal(0, errors.Count);

        errors = new ImportErrors();
        var row = Row(2, ("firstname", " SARA "), ("lastname", "khan"), ("workemail", "sara2@company.com"), ("department", "FIN"),
                      ("designation", "Accountant"), ("location", "KHI"), ("employmenttype", "Full time"),
                      ("joiningdate", "2026-01-05"), ("dateofbirth", "1980-01-01"));
        EmployeeImport.Plan([row], Snapshot(), errors);
        Assert.Contains(errors.Items, e => e.Code == ImportErrorCodes.DuplicatePerson && e.Args!["code"] == "EMP-0001");
    }

    [Fact]
    public void Manager_checks_self_unknown_exited_and_loops()
    {
        var errors = new ImportErrors();
        var plan = EmployeeImport.Plan(
        [
            Person(2, "Self", "self@company.com", manager: "SELF@company.com"),
            Person(3, "Nobody", "nobody@company.com", manager: "ghost@company.com"),
            Person(4, "Late", "late@company.com", manager: "EMP-0002"),
            Person(5, "Aa", "a@company.com", manager: "b@company.com"),
            Person(6, "Bb", "b@company.com", manager: "a@company.com"),
            Person(7, "Cc", "c@company.com", manager: "a@company.com")   // loop wale ke neeche
        ], Snapshot(), errors);

        Assert.Contains(errors.Items, e => e.Row == 2 && e.Code == ImportErrorCodes.SelfReference);
        Assert.Contains(errors.Items, e => e.Row == 3 && e.Code == ImportErrorCodes.NotFound);
        Assert.Contains(errors.Items, e => e.Row == 4 && e.Code == ImportErrorCodes.Inactive);
        Assert.Contains(errors.Items, e => e.Row == 5 && e.Code == ImportErrorCodes.Loop);
        Assert.Contains(errors.Items, e => e.Row == 6 && e.Code == ImportErrorCodes.Loop);
        Assert.Contains(errors.Items, e => e.Row == 7 && e.Code == ImportErrorCodes.RowInvalid);
        Assert.Empty(plan);
    }

    [Fact]
    public void Field_checks_report_every_problem_in_a_row()
    {
        var errors = new ImportErrors();
        var row = Row(2,
            ("firstname", "Ali2"), ("lastname", null), ("workemail", "not-an-email"), ("mobile", "abc"),
            ("department", "OLD"), ("designation", "Pilot"), ("location", "KHI"), ("employmenttype", "Freelance"),
            ("joiningdate", "2026-01-05"), ("probationenddate", "2025-12-01"), ("dateofbirth", "2020-01-01"), ("gender", "x"));
        EmployeeImport.Plan([row], Snapshot(), errors);

        var codes = errors.Items.Select(e => e.Code).ToHashSet();
        Assert.Contains(ImportErrorCodes.InvalidName, codes);
        Assert.Contains(ImportErrorCodes.Required, codes);
        Assert.Contains(ImportErrorCodes.InvalidEmail, codes);
        Assert.Contains(ImportErrorCodes.InvalidPhone, codes);
        Assert.Contains(ImportErrorCodes.Inactive, codes);        // OLD department
        Assert.Contains(ImportErrorCodes.NotFound, codes);        // Pilot
        Assert.Contains(ImportErrorCodes.InvalidChoice, codes);   // Freelance, gender x
        Assert.Contains(ImportErrorCodes.DateOrder, codes);       // probation
        Assert.Contains(ImportErrorCodes.AgeRange, codes);        // 6 saal
    }

    [Fact]
    public void Departments_resolve_parents_in_file_and_reject_loops()
    {
        var errors = new ImportErrors();
        var people = new EmployeeLookup([new EmployeeLookup.Person(Ceo, "EMP-0001", "ceo@hr-cloud.online", false)]);
        var plan = DepartmentImport.Plan(
        [
            Row(2, ("code", "ap"), ("name", "Payables"), ("parent", "acc")),
            Row(3, ("code", "ACC"), ("name", "Accounts"), ("parent", "fin"), ("head", "ceo@HR-cloud.online")),
            Row(4, ("code", "X1"), ("name", "Loop one"), ("parent", "X2")),
            Row(5, ("code", "X2"), ("name", "Loop two"), ("parent", "X1")),
            Row(6, ("code", "FIN"), ("name", "finance ")),
            Row(7, ("code", "Y"), ("name", "Orphan"), ("parent", "nope"))
        ], [new DepartmentImport.Existing(Fin, "FIN", "Finance")], people, errors);

        Assert.Equal(["ACC", "AP"], plan.Select(p => p.Code).ToArray());   // parent pehle
        Assert.Equal(Fin, plan[0].ParentId);
        Assert.Equal(Ceo, plan[0].HeadEmployeeId);
        Assert.Equal(3, plan[1].ParentRow);
        Assert.Contains(errors.Items, e => e.Row == 4 && e.Code == ImportErrorCodes.Loop);
        Assert.Contains(errors.Items, e => e.Row == 6 && e.Code == ImportErrorCodes.AlreadyExists && e.Column == "Code");
        Assert.Contains(errors.Items, e => e.Row == 6 && e.Code == ImportErrorCodes.AlreadyExists && e.Column == "Name");
        Assert.Contains(errors.Items, e => e.Row == 7 && e.Code == ImportErrorCodes.NotFound);
    }

    [Fact]
    public void Locations_check_country_time_zone_work_week_and_single_head_office()
    {
        var errors = new ImportErrors();
        var plan = LocationImport.Plan(
        [
            Row(2, ("code", "khi"), ("name", "Karachi HO"), ("country", "pk"), ("timezone", "Asia/Karachi"), ("workweek", "Mon-Sat"), ("headoffice", "Yes")),
            Row(3, ("code", "DXB"), ("name", "Dubai"), ("country", "UAE"), ("timezone", "Mars/Base"), ("workweek", "Funday"), ("headoffice", "yes")),
            Row(4, ("code", "RUH"), ("name", "Riyadh"), ("country", "SA"), ("timezone", "Asia/Riyadh"), ("workweek", "Sun-Thu"))
        ], [], errors);

        Assert.Equal(2, plan.Count);
        Assert.Equal("KHI", plan[0].Code);
        Assert.Equal("PK", plan[0].CountryCode);
        Assert.Equal(63, plan[0].WorkWeekDays);                     // Mon..Sat
        Assert.Equal(64 | 1 | 2 | 4 | 8, plan[1].WorkWeekDays);     // Sun..Thu
        var row3 = errors.Items.Where(e => e.Row == 3).Select(e => e.Code).Order().ToArray();
        string[] expected = [ImportErrorCodes.InvalidCountry, ImportErrorCodes.InvalidTimeZone, ImportErrorCodes.InvalidWorkWeek, ImportErrorCodes.MultipleHeadOffices];
        Assert.Equal(expected.Order().ToArray(), row3);
    }

    [Fact]
    public void Designations_are_unique_by_title_and_level_is_1_to_20()
    {
        var errors = new ImportErrors();
        var plan = DesignationImport.Plan(
        [
            Row(2, ("title", "Senior  Accountant"), ("level", "6")),
            Row(3, ("title", "senior accountant")),
            Row(4, ("title", "accountant")),
            Row(5, ("title", "CEO"), ("level", "25"))
        ], ["Accountant"], errors);

        Assert.Single(plan);
        Assert.Equal("Senior Accountant", plan[0].Title);   // double space saaf
        Assert.Contains(errors.Items, e => e.Row == 3 && e.Code == ImportErrorCodes.DuplicateInFile);
        Assert.Contains(errors.Items, e => e.Row == 4 && e.Code == ImportErrorCodes.AlreadyExists);
        Assert.Contains(errors.Items, e => e.Row == 5 && e.Code == ImportErrorCodes.InvalidNumber);
    }

    [Fact]
    public void Template_round_trips_and_reads_dates_and_skips_blank_rows()
    {
        var template = ImportWorkbook.Template(ImportKind.Employees, EmployeeImport.Columns,
            [new ImportChoiceList(EmployeeImport.Gender.Key, "Gender", ["Male", "Female", "Other"])]);

        using var wb = new XLWorkbook(new MemoryStream(template));
        var data = wb.Worksheet(ImportWorkbook.DataSheet);
        Assert.Equal("First name *", data.Cell(1, 2).GetString());
        data.Cell(2, 2).Value = "Ali";
        data.Cell(2, 5).Value = "ali@company.com";
        data.Cell(2, 15).Value = new DateTime(2026, 1, 5);
        data.Cell(4, 2).Value = "Moeen";   // row 3 khali
        using var saved = new MemoryStream();
        wb.SaveAs(saved);
        saved.Position = 0;

        var errors = new ImportErrors();
        var rows = ImportWorkbook.Read(saved, EmployeeImport.Columns, errors);
        Assert.Equal(0, errors.Count);
        Assert.Equal([2, 4], rows.Select(r => r.Number).ToArray());
        Assert.Equal("2026-01-05", rows[0][EmployeeImport.JoiningDate.Key]);
    }

    [Fact]
    public void Missing_required_column_and_bad_file_are_reported()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Data").Cell(1, 1).Value = "Title";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var errors = new ImportErrors();
        ImportWorkbook.Read(ms, EmployeeImport.Columns, errors);
        Assert.Contains(errors.Items, e => e.Code == ImportErrorCodes.MissingColumn && e.Column == "Work email");

        errors = new ImportErrors();
        ImportWorkbook.Read(new MemoryStream("hello"u8.ToArray()), EmployeeImport.Columns, errors);
        Assert.Equal(ImportErrorCodes.BadFile, Assert.Single(errors.Items).Code);
    }

    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        var template = ImportWorkbook.Template(ImportKind.Employees, EmployeeImport.Columns, []);
        using var wb = new XLWorkbook(new MemoryStream(template));
        fill(wb.Worksheet(ImportWorkbook.DataSheet));
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Columns_in_any_order_are_matched_by_name()
    {
        using var file = Workbook(data =>
        {
            // First name aur Last name ki jagah badal do
            data.Cell(1, 2).Value = "Last name *";
            data.Cell(1, 4).Value = "First name *";
            data.Cell(2, 2).Value = "Khan";
            data.Cell(2, 4).Value = "Ali";
            data.Cell(2, 5).Value = "ali@company.com";
        });

        var errors = new ImportErrors();
        var rows = ImportWorkbook.Read(file, EmployeeImport.Columns, errors);
        Assert.Equal(0, errors.Count);
        Assert.Equal("Ali", rows[0][EmployeeImport.FirstName.Key]);
        Assert.Equal("Khan", rows[0][EmployeeImport.LastName.Key]);
    }

    [Fact]
    public void Misspelled_and_repeated_columns_reject_the_file()
    {
        using var file = Workbook(data =>
        {
            data.Cell(1, 3).Value = "Midle name";                 // optional, ghalat spelling
            data.Cell(1, 30).Value = "Salary";                     // template mein hai hi nahi
            data.Cell(1, 31).Value = "first  NAME";                // First name dobara
            data.Cell(2, 2).Value = "Ali";
        });

        var errors = new ImportErrors();
        var rows = ImportWorkbook.Read(file, EmployeeImport.Columns, errors);
        Assert.Empty(rows);
        Assert.Contains(errors.Items, e => e.Code == ImportErrorCodes.UnknownColumn && e.Value == "Midle name"
                                           && e.Args!["suggestion"] == "Middle name");
        Assert.Contains(errors.Items, e => e.Code == ImportErrorCodes.UnknownColumn && e.Value == "Salary" && e.Args!["suggestion"] == "");
        Assert.Contains(errors.Items, e => e.Code == ImportErrorCodes.DuplicateColumn && e.Column == "First name");
    }

    [Theory]
    [InlineData("null")]
    [InlineData(" NULL ")]
    [InlineData("N/A")]
    [InlineData("undefined")]
    [InlineData("None")]
    public void Null_as_text_is_rejected(string text)
    {
        using var file = Workbook(data =>
        {
            data.Cell(2, 2).Value = "Ali";
            data.Cell(2, 4).Value = text;
            data.Cell(2, 5).Value = "ali@company.com";
        });

        var errors = new ImportErrors();
        var rows = ImportWorkbook.Read(file, EmployeeImport.Columns, errors);
        var error = Assert.Single(errors.Items);
        Assert.Equal(ImportErrorCodes.NullText, error.Code);
        Assert.Equal((2, "Last name"), (error.Row, error.Column));
        Assert.Null(rows[0][EmployeeImport.LastName.Key]);
    }

    [Theory]
    [InlineData("2026-01-31", 2026, 1, 31)]
    [InlineData("31/01/2026", 2026, 1, 31)]
    [InlineData("31-Jan-2026", 2026, 1, 31)]
    [InlineData("46053", 2026, 1, 31)]   // Excel serial
    public void Dates_accept_common_formats(string text, int y, int m, int d)
    {
        Assert.True(ImportText.TryDate(text, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Fact]
    public void Five_thousand_rows_plan_quickly()
    {
        var rows = Enumerable.Range(0, 5000)
            .Select(i => Person(i + 2, "Person", $"p{i}@company.com", manager: i == 0 ? null : "p0@company.com"))
            .ToList();
        var errors = new ImportErrors();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var plan = EmployeeImport.Plan(rows, Snapshot(), errors);
        Assert.Equal(0, errors.Count);
        Assert.Equal(5000, plan.Count);
        Assert.Equal(2, plan[0].Row);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Equal(EmploymentType.FullTime, plan[^1].EmploymentType);
    }
}
