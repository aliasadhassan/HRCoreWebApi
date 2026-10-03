using System.ComponentModel.DataAnnotations;
using HR.Identity.API.Data;
using HR.Shared.Library.Helpers;
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
public sealed class UpdateMeRequest
{
    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class MeController(AppDbContext db, AccessTokenFactory access, RefreshTokenService refreshTokens) : ControllerBase
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

    /// <summary>My settings: apna naam. Naya naam agle token refresh pe topbar mein.</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateMeRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user is null) return Unauthorized();

        user.DisplayName = request.DisplayName.Trim();
        user.UpdatedBy = userId;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>My settings: password badlo. Har device se logout — is browser mein bhi dobara login.</summary>
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var user = await db.Users.Include(u => u.Tenant).ThenInclude(t => t.Settings)
                                 .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user is null) return Unauthorized();

        if (user.PasswordHash is null)
            return Problem(statusCode: 409, detail: "You sign in with Microsoft, so there's no password to change here.");
        if (!PasswordHelper.Verify(request.CurrentPassword, user.PasswordHash))
            return Problem(statusCode: 400, detail: "Your current password isn't correct.");

        var minLength = user.Tenant.Settings?.PasswordMinLength ?? 8;
        if (request.NewPassword.Length < minLength)
            return Problem(statusCode: 400, detail: $"Password must be at least {minLength} characters.");

        user.PasswordHash = PasswordHelper.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        user.MustChangePassword = false;
        user.UpdatedBy = userId;
        await db.SaveChangesAsync(ct);
        await refreshTokens.RevokeAllForUserAsync(user.Id, "PasswordChanged");
        return NoContent();
    }
}
