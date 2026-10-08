namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Audit page ka "people" hissa. Gateway: /audit/{everything}. Poori company ka itihaas, isliye settings.view
/// (Login activity jaisa). Log AppDbContext.SaveChanges khud likhta hai, yahan sirf parhna.
/// </summary>
[Route("api/audit")]
[HasPermission(Permissions.SettingsView)]
public sealed class AuditController(IAppDbContext db, ICurrentUser currentUser) : AuditControllerBase
{
    protected override IQueryable<AuditLog> Logs()
    {
        var tenantId = currentUser.RequireTenantId();
        return db.AuditLogs.AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    protected override Func<IReadOnlyCollection<Guid>, CancellationToken, Task<Dictionary<Guid, string>>> SubjectNames => SubjectNamesAsync;

    // Hataye gaye (soft delete) employees ka naam bhi chahiye — audit mein wohi to dikhte hain
    private async Task<Dictionary<Guid, string>> SubjectNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();
        return (await db.Employees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId && ids.Contains(e.Id))
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeCode })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName} · {e.EmployeeCode}");
    }
}
