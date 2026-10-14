namespace HR.Employee.API.Application.Imports;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Excel upload → poori file pehle check, phir sab rows EK SaveChanges (ek transaction) mein.
/// SQL table-type / DataTable jaisa: EF Npgsql inserts ko batches mein bhejta hai (har round trip mein saikron rows),
/// row-by-row nahi. Koi bhi row ghalat ho to kuch save nahi hota (all-or-nothing). DryRun = sirf check.
/// </summary>
public sealed record RunImportCommand(ImportKind Kind, byte[] File, bool DryRun) : IRequest<ImportResult>;

public sealed record GetImportTemplateQuery(ImportKind Kind) : IRequest<byte[]>;

public sealed class ImportHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<RunImportCommand, ImportResult>,
    IRequestHandler<GetImportTemplateQuery, byte[]>
{
    public static IReadOnlyList<ImportColumn> ColumnsFor(ImportKind kind) => kind switch
    {
        ImportKind.Locations => LocationImport.Columns,
        ImportKind.Departments => DepartmentImport.Columns,
        ImportKind.Designations => DesignationImport.Columns,
        ImportKind.Employees => EmployeeImport.Columns,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public async Task<ImportResult> Handle(RunImportCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();
        var errors = new ImportErrors();

        IReadOnlyList<ImportRow> rows;
        using (var stream = new MemoryStream(request.File, writable: false))
            rows = ImportWorkbook.Read(stream, ColumnsFor(request.Kind), errors);

        if (errors.Count > 0)
            return Result(request, rows.Count, 0, errors);

        // 5,000 rows ka ek SaveChanges default 30 sec se lamba ho sakta hai
        if (db is DbContext context)
            context.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

        var imported = request.Kind switch
        {
            ImportKind.Locations => await ImportLocationsAsync(rows, errors, request.DryRun, ct),
            ImportKind.Departments => await ImportDepartmentsAsync(rows, errors, request.DryRun, ct),
            ImportKind.Designations => await ImportDesignationsAsync(rows, errors, request.DryRun, ct),
            ImportKind.Employees => await ImportEmployeesAsync(tenantId, rows, errors, request.DryRun, ct),
            _ => 0
        };

        return Result(request, rows.Count, imported, errors);
    }

    private static ImportResult Result(RunImportCommand request, int totalRows, int imported, ImportErrors errors)
        => new(request.Kind, request.DryRun, errors.Count == 0, totalRows, imported, errors.Count,
               errors.Items.OrderBy(e => e.Row).Take(ImportResult.MaxErrors).ToList());

    // ─────────────────────────────── Locations ───────────────────────────────
    private async Task<int> ImportLocationsAsync(IReadOnlyList<ImportRow> rows, ImportErrors errors, bool dryRun, CancellationToken ct)
    {
        var existing = await db.Locations.AsNoTracking()
            .Select(x => new LocationImport.Existing(x.Code, x.Name))
            .ToListAsync(ct);

        var plan = LocationImport.Plan(rows, existing, errors);
        if (errors.Count > 0 || dryRun)
            return 0;

        var tenantId = currentUser.RequireTenantId();
        if (plan.Any(p => p.IsHeadOffice))
            foreach (var other in await db.Locations.Where(x => x.IsHeadOffice).ToListAsync(ct))
                other.SetHeadOffice(false);   // head office sirf ek

        db.Locations.AddRange(plan.Select(p => Location.Create(
            tenantId, p.Name, p.Code, p.CountryCode, p.TimeZone, p.City, p.Address, p.WorkWeekDays, p.IsHeadOffice)));

        await db.SaveChangesAsync(ct);
        return plan.Count;
    }

    // ─────────────────────────────── Departments ─────────────────────────────
    private async Task<int> ImportDepartmentsAsync(IReadOnlyList<ImportRow> rows, ImportErrors errors, bool dryRun, CancellationToken ct)
    {
        var existing = await db.Departments.AsNoTracking()
            .Select(x => new DepartmentImport.Existing(x.Id, x.Code, x.Name))
            .ToListAsync(ct);
        var people = await LoadPeopleAsync(ct);

        var plan = DepartmentImport.Plan(rows, existing, new EmployeeLookup(people), errors);
        if (errors.Count > 0 || dryRun)
            return 0;

        var tenantId = currentUser.RequireTenantId();
        var idByRow = new Dictionary<int, Guid>();
        foreach (var p in plan)   // parents pehle aate hain
        {
            var parentId = p.ParentRow is { } row ? idByRow[row] : p.ParentId;
            var department = Department.Create(tenantId, p.Name, p.Code, p.Description, parentId);
            department.AssignHead(p.HeadEmployeeId);
            db.Departments.Add(department);   // Add pe Id ban jati hai
            idByRow[p.Row] = department.Id;
        }

        await db.SaveChangesAsync(ct);
        return plan.Count;
    }

    // ─────────────────────────────── Designations ────────────────────────────
    private async Task<int> ImportDesignationsAsync(IReadOnlyList<ImportRow> rows, ImportErrors errors, bool dryRun, CancellationToken ct)
    {
        var existing = await db.Designations.AsNoTracking().Select(x => x.Title).ToListAsync(ct);

        var plan = DesignationImport.Plan(rows, existing, errors);
        if (errors.Count > 0 || dryRun)
            return 0;

        var tenantId = currentUser.RequireTenantId();
        db.Designations.AddRange(plan.Select(p => Designation.Create(tenantId, p.Title, p.Level, p.Description)));

        await db.SaveChangesAsync(ct);
        return plan.Count;
    }

    // ─────────────────────────────── Employees ───────────────────────────────
    private async Task<int> ImportEmployeesAsync(Guid tenantId, IReadOnlyList<ImportRow> rows, ImportErrors errors, bool dryRun, CancellationToken ct)
    {
        var employees = await db.Employees.AsNoTracking()
            .Select(e => new EmployeeImport.ExistingEmployee(
                e.Id, e.EmployeeCode, e.WorkEmail, e.FirstName, e.LastName, e.DateOfBirth, e.EmploymentStatus == EmploymentStatus.Exited))
            .ToListAsync(ct);

        // Soft-deleted ke codes bhi — naya auto code purane se na takraye
        var allCodes = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.EmployeeCode)
            .ToListAsync(ct);

        var snapshot = new EmployeeImport.Snapshot(
            employees, allCodes, allCodes.Count,
            await db.Departments.AsNoTracking().Select(x => new EmployeeImport.OrgRef(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct),
            await db.Designations.AsNoTracking().Select(x => new EmployeeImport.OrgRef(x.Id, null, x.Title, x.IsActive)).ToListAsync(ct),
            await db.Locations.AsNoTracking().Select(x => new EmployeeImport.OrgRef(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct),
            DateOnly.FromDateTime(DateTime.UtcNow));

        var plan = EmployeeImport.Plan(rows, snapshot, errors);
        if (errors.Count > 0 || dryRun)
            return 0;

        var idByRow = new Dictionary<int, Guid>();
        foreach (var p in plan)   // managers pehle aate hain
        {
            var managerId = p.ManagerRow is { } row ? idByRow[row] : p.ManagerId;
            var employee = Employee.Create(
                tenantId, p.Code, p.FirstName, p.MiddleName, p.LastName, p.WorkEmail,
                p.LocationId, p.DepartmentId, p.DesignationId, managerId,
                p.EmploymentType, p.JoiningDate, p.ProbationEndDate);

            if (p.Gender is not null || p.DateOfBirth is not null)
                employee.UpdatePersonalInfo(p.FirstName, p.MiddleName, p.LastName, p.Gender, p.DateOfBirth, null, null);
            if (p.PersonalEmail is not null || p.Mobile is not null)
                employee.UpdateContactInfo(p.PersonalEmail, null, p.Mobile, null);

            db.Employees.Add(employee);
            idByRow[p.Row] = employee.Id;
        }

        // Har employee ka EmployeeCreated event isi transaction mein outbox mein (Payroll ko bhi pata chale)
        await db.SaveChangesAsync(ct);
        return plan.Count;
    }

    private async Task<List<EmployeeLookup.Person>> LoadPeopleAsync(CancellationToken ct)
        => await db.Employees.AsNoTracking()
            .Select(e => new EmployeeLookup.Person(e.Id, e.EmployeeCode, e.WorkEmail, e.EmploymentStatus == EmploymentStatus.Exited))
            .ToListAsync(ct);

    // ─────────────────────────────── Template ────────────────────────────────
    public async Task<byte[]> Handle(GetImportTemplateQuery request, CancellationToken ct)
    {
        currentUser.RequireTenantId();
        var lists = new List<ImportChoiceList>();

        switch (request.Kind)
        {
            case ImportKind.Locations:
                lists.Add(new ImportChoiceList(LocationImport.HeadOffice.Key, "Head office", ["Yes", "No"]));
                lists.Add(new ImportChoiceList(LocationImport.WorkWeek.Key, "Work week", ["Mon-Fri", "Mon-Sat", "Sun-Thu", "Mon-Sun"]));
                break;

            case ImportKind.Departments:
                lists.Add(new ImportChoiceList(DepartmentImport.Parent.Key, "Departments (code)",
                    await db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(ct)));
                break;

            case ImportKind.Employees:
                lists.Add(new ImportChoiceList(EmployeeImport.Department.Key, "Departments (code)",
                    await db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(ct)));
                lists.Add(new ImportChoiceList(EmployeeImport.Designation.Key, "Designations",
                    await db.Designations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Title).Select(x => x.Title).ToListAsync(ct)));
                lists.Add(new ImportChoiceList(EmployeeImport.Location.Key, "Locations (code)",
                    await db.Locations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(ct)));
                lists.Add(new ImportChoiceList(EmployeeImport.EmploymentType.Key, "Employment type", ["Full time", "Part time", "Contract", "Intern"]));
                lists.Add(new ImportChoiceList(EmployeeImport.Gender.Key, "Gender", ["Male", "Female", "Other"]));
                break;
        }

        return ImportWorkbook.Template(request.Kind, ColumnsFor(request.Kind), lists);
    }
}
