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
    protected override IQueryable<AuditLog> Source => db.AuditLogs;

    protected override Guid CurrentTenantId() => currentUser.RequireTenantId();

    protected override void EnsureAllowed()
    {
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !currentUser.HasPermission(Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("You do not have permission to see the payroll audit trail.");
    }

    protected override IQueryable<AuditSubject> Subjects
    {
        get
        {
            var tenantId = currentUser.RequireTenantId();
            return db.PayrollEmployees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId)
                .Select(e => new AuditSubject { Id = e.Id, Name = e.FullName + " · " + e.EmployeeCode });
        }
    }
}
