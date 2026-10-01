using HR.Identity.API.Data;
using HR.Identity.API.Models.Admin;
using HR.Identity.API.Models.Common;
using HR.Shared.Library.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Controllers;

/// <summary>
/// Company ki sign-in history (LoginAudit). Sirf parhna — audit kabhi edit nahi hota.
/// Note: jis email ka koi user hi nahi (UserNotFound) uska TenantId NULL hota hai, is liye wo yahan nahi aata.
/// </summary>
[ApiController]
[Route("api/login-activity")]
[HasPermission(Permissions.SettingsView)]
public sealed class LoginActivityController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => User.GetTenantId() ?? throw new UnauthorizedAccessException("Tenant missing in token.");

    [HttpGet]
    public async Task<ActionResult<PagedDto<LoginActivityItemDto>>> List(
        [FromQuery] Guid? userId,
        [FromQuery] string? search,
        [FromQuery] bool? succeeded,
        [FromQuery] LoginMethod? method,
        [FromQuery] int days = 7,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        days = Math.Clamp(days, 1, 365);

        var tenantId = TenantId;
        var since = DateTime.UtcNow.AddDays(-days);

        var query = db.LoginAudits.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.OccurredAt >= since);

        if (userId is { } uid) query = query.Where(a => a.UserId == uid);
        if (succeeded is { } ok) query = query.Where(a => a.Succeeded == ok);
        if (method is { } m) query = query.Where(a => a.Method == m);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.EmailAttempted.Contains(term)
                                     || db.Users.Any(u => u.Id == a.UserId && u.DisplayName.Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new LoginActivityItemDto(
                a.Id,
                a.OccurredAt,
                a.UserId,
                db.Users.Where(u => u.Id == a.UserId).Select(u => u.DisplayName).FirstOrDefault(),
                a.EmailAttempted,
                a.Method,
                a.Succeeded,
                a.FailureReason,
                a.IpAddress,
                a.UserAgent))
            .ToListAsync(ct);

        return Ok(new PagedDto<LoginActivityItemDto>(items, total, page, pageSize));
    }

    /// <summary>Upar wale numbers: kitne sign-in, kitne fail, kitne lockout, kitne alag log.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<LoginActivitySummaryDto>> Summary([FromQuery] int days = 7, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 365);
        var tenantId = TenantId;
        var since = DateTime.UtcNow.AddDays(-days);
        var q = db.LoginAudits.AsNoTracking().Where(a => a.TenantId == tenantId && a.OccurredAt >= since);

        return Ok(new LoginActivitySummaryDto(
            days,
            await q.CountAsync(a => a.Succeeded, ct),
            await q.CountAsync(a => !a.Succeeded, ct),
            await q.CountAsync(a => !a.Succeeded && a.FailureReason == "LockedOut", ct),
            await q.Where(a => a.Succeeded && a.UserId != null).Select(a => a.UserId).Distinct().CountAsync(ct)));
    }
}
