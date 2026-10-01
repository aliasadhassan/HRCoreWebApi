using HR.Identity.API.Data;
using HR.Identity.API.Services;
using HR.Shared.Library.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Controllers;

public sealed record MeTenantDto(Guid Id, string Name, string? LogoUrl);

public sealed record MeDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool MustChangePassword,
    bool HasPassword,
    MeTenantDto Tenant,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Logged-in user ki apni maloomat — DB se taaza (token purana ho sakta hai).
/// Angular isse sidebar/route guards aur profile menu bharta hai.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(AppDbContext db, AccessTokenFactory access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeDto>> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var user = await db.Users.AsNoTracking()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

        if (user is null)
            return Unauthorized();

        var rights = await access.GetAccessAsync(user.Id, ct);

        return Ok(new MeDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.MustChangePassword,
            user.PasswordHash is not null,
            new MeTenantDto(user.Tenant.Id, user.Tenant.Name, user.Tenant.LogoUrl),
            rights.Roles,
            rights.Permissions));
    }
}
