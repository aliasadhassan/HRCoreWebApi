using HR.Identity.API.Data;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace HR.Identity.API.Controllers;

/// <summary>
/// Audit page ka "access" hissa: users, roles, permissions, company settings ke badlaav. Sirf parhna.
/// Gateway: /audit/access/{everything} → yahan. Identity pe RLS nahi, isliye tenant filter yahin.
/// </summary>
[Route("api/audit")]
[HasPermission(Permissions.SettingsView)]
public sealed class AuditController(AppDbContext db) : AuditControllerBase
{
    protected override IQueryable<AuditLog> Source => db.AuditLogs;

    protected override Guid CurrentTenantId()
        => User.GetTenantId() ?? throw new UnauthorizedAccessException("Tenant missing in token.");
}
