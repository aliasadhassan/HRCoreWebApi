namespace HR.Employee.API.Application.Learning;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Shared.Library.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Learning &amp; growth ki ijazatein. employees.edit = catalogue, skill library, career roles chalaye aur kisi ko bhi assign kare.
/// employees.view = sab ki learning/skills dekhe. Manager = apne direct reports ko assign kare, unki skills assess kare.
/// Har employee = apni learning, apni skills (self rating) aur apna career goal.
/// </summary>
public sealed class LearningAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Guid? me;
    private bool meLoaded;
    private List<Guid>? reports;

    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit);
    public bool CanSeeAll => CanManage || currentUser.HasPermission(Permissions.EmployeesView);
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
        => await MeAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");

    /// <summary>Mere direct reports (active). Manager na ho to khali.</summary>
    public async Task<IReadOnlyList<Guid>> ReportsAsync(CancellationToken ct)
    {
        if (reports is not null)
            return reports;
        var my = await MeAsync(ct);
        reports = my is null
            ? []
            : await db.Employees.AsNoTracking().Where(e => e.ManagerId == my && e.EmploymentStatus != EmploymentStatus.Exited)
                .Select(e => e.Id).ToListAsync(ct);
        return reports;
    }

    public async Task<bool> IsManagerAsync(CancellationToken ct) => (await ReportsAsync(ct)).Count > 0;

    /// <summary>Assign, due date, assessment: HR (manage) ya us employee ka manager.</summary>
    public async Task<bool> CanLeadAsync(Guid employeeId, CancellationToken ct)
        => CanManage || (await ReportsAsync(ct)).Contains(employeeId);

    public async Task<bool> CanSeeAsync(Guid employeeId, CancellationToken ct)
        => CanSeeAll || employeeId == await MeAsync(ct) || (await ReportsAsync(ct)).Contains(employeeId);

    public async Task EnsureCanSeeAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await CanSeeAsync(employeeId, ct))
            throw new UnauthorizedAccessException("You can only see your own learning and your team's.");
    }

    /// <summary>Team scope: HR = sab, manager = reports, warna kuch nahi.</summary>
    public async Task<IQueryable<Employee>> TeamAsync(CancellationToken ct)
    {
        var people = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited);
        if (CanSeeAll)
            return people;
        var ids = await ReportsAsync(ct);
        return people.Where(e => ids.Contains(e.Id));
    }

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage learning and skills.");
    }
}
