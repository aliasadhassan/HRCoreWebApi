namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.Common.Interfaces;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Audit page ka "payroll" hissa. Gateway: /payroll/audit/{everything}. Tankhwah ka itihaas hai,
/// isliye payroll.view.all ya payroll.approve (settings.view kaafi nahi).
/// </summary>
[Route("api/payroll/audit")]
public sealed class PayrollAuditController(IAppDbContext db, ICurrentUser currentUser) : AuditControllerBase
{
    protected override IQueryable<AuditLog> Logs()
    {
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !currentUser.HasPermission(Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("You do not have permission to see the payroll audit trail.");
        var tenantId = currentUser.RequireTenantId();
        return db.AuditLogs.AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    protected override Func<IReadOnlyCollection<Guid>, CancellationToken, Task<Dictionary<Guid, string>>> SubjectNames => SubjectNamesAsync;

    private async Task<Dictionary<Guid, string>> SubjectNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.RequireTenantId();
        return (await db.PayrollEmployees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId && ids.Contains(e.Id))
                .Select(e => new { e.Id, e.FullName, e.EmployeeCode })
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.Id, e => $"{e.FullName} · {e.EmployeeCode}");
    }
}
