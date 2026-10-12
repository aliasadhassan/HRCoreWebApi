namespace HR.Employee.API.Application.Engagement;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Shared.Library.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Engagement ki ijazatein. employees.edit = surveys banaye/chalaye aur kisi bhi recognition ko hata sake.
/// employees.view = survey results dekhe. Har employee = apne surveys ka jawab de, recognition de/dekhe, cheer kare.
/// </summary>
public sealed class EngagementAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private (Guid Id, Guid DepartmentId)? me;
    private bool meLoaded;

    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit);
    public bool CanSeeResults => CanManage || currentUser.HasPermission(Permissions.EmployeesView);
    public Guid TenantId => currentUser.RequireTenantId();

    public async Task<(Guid Id, Guid DepartmentId)?> MeAsync(CancellationToken ct)
    {
        if (meLoaded || currentUser.UserId is not { } userId)
            return me;
        meLoaded = true;
        var row = await db.Employees.AsNoTracking().Where(e => e.UserId == userId && e.EmploymentStatus != EmploymentStatus.Exited)
            .Select(e => new { e.Id, e.DepartmentId }).FirstOrDefaultAsync(ct);
        me = row is null ? null : (row.Id, row.DepartmentId);
        return me;
    }

    public async Task<(Guid Id, Guid DepartmentId)> RequireMeAsync(CancellationToken ct)
        => await MeAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage surveys.");
    }

    public void EnsureCanSeeResults()
    {
        if (!CanSeeResults)
            throw new UnauthorizedAccessException("You do not have permission to see survey results.");
    }
}

internal static class EngagementClock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
