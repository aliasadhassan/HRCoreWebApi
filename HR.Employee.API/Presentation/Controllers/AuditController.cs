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
    protected override IQueryable<AuditLog> Source => db.AuditLogs;

    protected override Guid CurrentTenantId() => currentUser.RequireTenantId();

    // Hataye gaye (soft delete) employees ka naam bhi chahiye — audit mein wohi to dikhte hain
    protected override IQueryable<AuditSubject> Subjects
    {
        get
        {
            var tenantId = currentUser.RequireTenantId();
            return db.Employees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId)
                .Select(e => new AuditSubject { Id = e.Id, Name = e.FirstName + " " + e.LastName + " · " + e.EmployeeCode });
        }
    }
}
