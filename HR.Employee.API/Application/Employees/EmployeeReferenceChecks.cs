namespace HR.Employee.API.Application.Employees;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using Microsoft.EntityFrameworkCore;

/// <summary>Create/ChangeJob dono mein same checks — tenant filter ki wajah se doosri company ka record milega hi nahi.</summary>
internal static class EmployeeReferenceChecks
{
    public static async Task EnsureJobReferencesAsync(
        IAppDbContext db, Guid locationId, Guid departmentId, Guid designationId, Guid? managerId, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(x => x.Id == locationId && x.IsActive, ct))
            throw new NotFoundException("Location", locationId);
        if (!await db.Departments.AnyAsync(x => x.Id == departmentId && x.IsActive, ct))
            throw new NotFoundException("Department", departmentId);
        if (!await db.Designations.AnyAsync(x => x.Id == designationId && x.IsActive, ct))
            throw new NotFoundException("Designation", designationId);

        if (managerId is { } id &&
            !await db.Employees.AnyAsync(e => e.Id == id && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new NotFoundException("Manager", id);
    }

    /// <summary>A → B → C → A jaisa reporting loop na bane.</summary>
    public static async Task EnsureNoManagerCycleAsync(IAppDbContext db, Guid employeeId, Guid? newManagerId, CancellationToken ct)
    {
        var current = newManagerId;
        for (var depth = 0; current is not null && depth < 50; depth++)
        {
            if (current == employeeId)
                throw new ConflictException("This manager assignment would create a reporting loop.");

            var id = current.Value;
            current = await db.Employees.Where(e => e.Id == id).Select(e => e.ManagerId).FirstOrDefaultAsync(ct);
        }
    }

    public static async Task<Employee> GetEmployeeAsync(IAppDbContext db, Guid employeeId, CancellationToken ct)
        => await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
           ?? throw new NotFoundException("Employee", employeeId);
}
