namespace HR.Employee.API.Application.Safety;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Safety;
using HR.Shared.Library.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Health &amp; safety ki ijazatein.
/// safety.manage = safety team: sab incidents, actions, inspections, checklists.
/// Investigator (incident par naam) = us incident par notes, status, root cause, actions — bina safety.manage ke bhi.
/// Assignee = apna action shuru / mukammal. Inspector = apni inspection mukammal.
/// Har employee incident report kar sakta hai aur apni reports ka status dekh sakta hai.
/// </summary>
public sealed class SafetyAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Guid? me;
    private bool meLoaded;

    public bool CanManage => currentUser.HasPermission(Permissions.SafetyManage);
    public Guid? UserId => currentUser.UserId;
    public Guid TenantId => currentUser.RequireTenantId();

    public async Task<Guid?> MeAsync(CancellationToken ct)
    {
        if (meLoaded || currentUser.UserId is not { } userId)
            return me;
        meLoaded = true;
        var id = await db.Employees.AsNoTracking().Where(e => e.UserId == userId && e.EmploymentStatus != EmploymentStatus.Exited)
            .Select(e => e.Id).FirstOrDefaultAsync(ct);
        me = id == Guid.Empty ? null : id;
        return me;
    }

    public async Task<Guid> RequireMeAsync(CancellationToken ct)
        => await MeAsync(ct) ?? throw new DomainException("Your login is not linked to an employee record. Ask HR to link it.");

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage health and safety.");
    }

    /// <summary>Incident par kaam: safety team ya us ka investigator.</summary>
    public async Task<SafetyIncident> WorkIncidentAsync(Guid id, CancellationToken ct, bool manageOnly = false)
    {
        var i = await db.SafetyIncidents.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Incident", id);
        if (CanManage)
            return i;
        var my = await MeAsync(ct);
        if (my is null || i.InvestigatorEmployeeId != my)
            throw new NotFoundException("Incident", id);
        if (manageOnly)
            throw new UnauthorizedAccessException("Only the safety team can do this.");
        return i;
    }

    public async Task<bool> CanSeeIncidentAsync(SafetyIncident i, CancellationToken ct)
        => CanManage || (await MeAsync(ct) is { } my && i.InvestigatorEmployeeId == my);
}

internal static class SafetyClock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
