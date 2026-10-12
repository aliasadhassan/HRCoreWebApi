namespace HR.Employee.API.Application.Relations;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Relations;
using HR.Shared.Library.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Employee relations ki ijazatein. Sirf HR role kaafi nahi: case-level access chalta hai.
/// relations.manage = relations team: Standard cases dekhe, case banaye, khaali case le (Take), stages chalaye.
/// Restricted case = sirf members (Owner / Investigator / Viewer); team ko list mein sirf masked line dikhti hai.
/// Member hona = access, chahe relations.manage na ho (e.g. senior manager ko viewer banaya).
/// Admin (settings.manage + relations.manage) = break-glass (wajah ke saath, access log mein).
/// Jo shakhs case mein involved ho (reporter, complainant, respondent, witness) woh team view mein case nahi dekhta.
/// </summary>
public sealed class RelationsAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Guid? me;
    private bool meLoaded;

    public bool CanManage => currentUser.HasPermission(Permissions.RelationsManage);
    public bool IsAdmin => CanManage && currentUser.HasPermission(Permissions.SettingsManage);
    public Guid? UserId => currentUser.UserId;
    public string? UserName => currentUser.Name;
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
            throw new UnauthorizedAccessException("You do not have permission to manage employee relations cases.");
    }

    /// <summary>Kya ye shakhs case mein involved hai (reporter ya party)? Phir team view band.</summary>
    public static bool IsInvolved(ErCase c, Guid? my)
        => my is { } id && (c.ReporterEmployeeId == id || c.Parties.Any(p => p.EmployeeId == id));

    /// <summary>Team view: member, ya relations team + standard. Restricted non-member = masked.</summary>
    public CaseView ViewOf(ErCase c, Guid? my)
    {
        if (IsInvolved(c, my))
            return CaseView.None;
        if (c.RoleOf(my) is { } role)
            return role switch
            {
                CaseMemberRole.Owner => CaseView.Owner,
                CaseMemberRole.Investigator => CaseView.Investigator,
                _ => CaseView.Viewer
            };
        if (!CanManage)
            return CaseView.None;
        return c.Confidentiality == CaseConfidentiality.Standard ? CaseView.Team : CaseView.Masked;
    }

    /// <summary>Team list ki base query: involved cases nikaal kar, member ya relations team.</summary>
    public IQueryable<ErCase> TeamCases(Guid? my)
    {
        var canManage = CanManage;
        return db.ErCases.AsNoTracking()
            .Where(c => my == null || (c.ReporterEmployeeId != my && !db.CaseParties.Any(p => p.CaseId == c.Id && p.EmployeeId == my)))
            .Where(c => canManage || (my != null && db.CaseMembers.Any(m => m.CaseId == c.Id && m.EmployeeId == my)));
    }

    public async Task<ErCase> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = db.ErCases.Include(c => c.Members).Include(c => c.Parties).Include(c => c.Evidence).AsSplitQuery();
        if (!tracking)
            q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Case", id);
    }

    /// <summary>Case par kaam (notes, stage, evidence) = owner ya investigator.</summary>
    public async Task<(ErCase Case, Guid Me, CaseView View)> WorkAsync(Guid id, CancellationToken ct, bool ownerOnly = false)
    {
        var c = await LoadAsync(id, ct);
        var my = await MeAsync(ct);
        var view = ViewOf(c, my);
        if (view is CaseView.None)
            throw new NotFoundException("Case", id);
        var allowed = ownerOnly ? view == CaseView.Owner : view is CaseView.Owner or CaseView.Investigator;
        if (!allowed || my is null)
            throw new UnauthorizedAccessException(ownerOnly
                ? "Only the case owner can do this."
                : "Only the case owner or an investigator can do this.");
        return (c, my.Value, view);
    }
}

public enum CaseView : byte { None = 0, Masked = 1, Team = 2, Viewer = 3, Investigator = 4, Owner = 5 }

internal static class RelationsClock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
