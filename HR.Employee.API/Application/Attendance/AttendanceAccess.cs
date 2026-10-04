namespace HR.Employee.API.Application.Attendance;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Shared.Library.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Kaun kya dekh/badal sakta hai (abhi existing permissions se, alag attendance permissions baad mein):
///   - apna data: har logged-in employee
///   - team (direct reports): manager
///   - sab: employees.view (dekhna), employees.edit / settings.manage (roster + HR edits)
/// </summary>
public sealed class AttendanceAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public bool CanViewAll => currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit) || currentUser.HasPermission(Permissions.SettingsManage);

    /// <summary>Token ka user → Employee (Employee.UserId link). Na mile to 403.</summary>
    public async Task<CurrentEmployee> CurrentEmployeeAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId
                     ?? throw new UnauthorizedAccessException("User information is missing from the access token.");
        return await db.Employees.AsNoTracking()
                   .Where(e => e.UserId == userId)
                   .Select(e => new CurrentEmployee(e.Id, e.LocationId, e.DepartmentId, e.ManagerId))
                   .FirstOrDefaultAsync(ct)
               ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");
    }

    public async Task<CurrentEmployee?> TryCurrentEmployeeAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return null;
        return await db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => new CurrentEmployee(e.Id, e.LocationId, e.DepartmentId, e.ManagerId))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Dekh sakta hai? (khud, apna report, ya view-all)</summary>
    public async Task EnsureCanViewAsync(Guid employeeId, CancellationToken ct)
    {
        if (CanViewAll)
            return;
        var me = await CurrentEmployeeAsync(ct);
        if (me.Id == employeeId)
            return;
        if (await db.Employees.AnyAsync(e => e.Id == employeeId && e.ManagerId == me.Id, ct))
            return;
        throw new UnauthorizedAccessException("You can only view your own or your team's attendance.");
    }

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to change attendance for other employees.");
    }
}

public sealed record CurrentEmployee(Guid Id, Guid LocationId, Guid DepartmentId, Guid? ManagerId);
