using System.ComponentModel.DataAnnotations;
using HR.Identity.API.Data;
using HR.Identity.API.Helpers;
using HR.Identity.API.Models.Common;
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
    IReadOnlyList<string> Permissions,
    int PasswordMinLength,
    DateTime? LastLoginAt);

public sealed record MeSessionDto(
    Guid Id,
    DateTime StartedAt,
    DateTime LastActiveAt,
    DateTime ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);

public sealed record MeSignInDto(
    long Id,
    DateTime OccurredAt,
    LoginMethod Method,
    bool Succeeded,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent);

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

/// <summary>
/// Logged-in user ki apni maloomat — DB se taaza (token purana ho sakta hai).
/// Angular My settings page isse bharta hai: profile, password, sessions, sign-in history.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(AppDbContext db, AccessTokenFactory access, RefreshTokenService refreshTokens) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeDto>> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var user = await db.Users.AsNoTracking()
            .Include(u => u.Tenant).ThenInclude(t => t.Settings)
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
            rights.Permissions,
            user.Tenant.Settings?.PasswordMinLength ?? 8,
            user.LastLoginAt));
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

    // ─────────────────────────────── SESSIONS ───────────────────────────────
    // Session = refresh-token family (har login/SSO nayi family, refresh pe usi mein rotate).
    // Active family = jis mein abhi ek unrevoked, unexpired token hai.

    private const string RefreshCookie = "X-Refresh-Token";   // AuthController jaisa

    private async Task<Guid?> CurrentFamilyAsync(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var raw) || string.IsNullOrEmpty(raw))
            return null;
        var hash = TokenHasher.Hash(raw);
        return await db.RefreshTokens.Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId).FirstOrDefaultAsync(ct);
    }

    /// <summary>My settings: jin devices pe main signed in hoon.</summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<MeSessionDto>>> Sessions(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var now = DateTime.UtcNow;
        var current = await CurrentFamilyAsync(ct);

        var active = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .Select(t => new { t.FamilyId, t.CreatedAt, t.ExpiresAt, t.CreatedByIp, t.UserAgent })
            .ToListAsync(ct);

        var familyIds = active.Select(a => a.FamilyId).Distinct().ToList();
        var started = await db.RefreshTokens.AsNoTracking()
            .Where(t => familyIds.Contains(t.FamilyId))
            .GroupBy(t => t.FamilyId)
            .Select(g => new { FamilyId = g.Key, StartedAt = g.Min(t => t.CreatedAt) })
            .ToDictionaryAsync(x => x.FamilyId, x => x.StartedAt, ct);

        var sessions = active
            .GroupBy(a => a.FamilyId)
            .Select(g => g.OrderByDescending(a => a.CreatedAt).First())
            .Select(a => new MeSessionDto(
                a.FamilyId,
                started.GetValueOrDefault(a.FamilyId, a.CreatedAt),
                a.CreatedAt,
                a.ExpiresAt,
                a.CreatedByIp,
                a.UserAgent,
                a.FamilyId == current))
            .OrderByDescending(s => s.IsCurrent).ThenByDescending(s => s.LastActiveAt)
            .ToList();

        return Ok(sessions);
    }

    /// <summary>Ek device ko sign out. Apna current session yahan se band nahi hota (uske liye logout).</summary>
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        if (id == await CurrentFamilyAsync(ct))
            return Problem(statusCode: 400, detail: "This is the device you're using. Use Sign out instead.");

        var now = DateTime.UtcNow;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var revoked = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.FamilyId == id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedByIp, ip)
                .SetProperty(t => t.RevokedReason, "SignedOutByUser"), ct);

        return revoked == 0 ? NotFound() : NoContent();
    }

    /// <summary>Is device ke ilawa har jagah se sign out.</summary>
    [HttpPost("sessions/revoke-others")]
    public async Task<ActionResult<object>> RevokeOtherSessions(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var current = await CurrentFamilyAsync(ct);
        var now = DateTime.UtcNow;

        var revoked = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.FamilyId != current)
            .Select(t => t.FamilyId).Distinct().CountAsync(ct);

        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.FamilyId != current)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, "SignedOutOthers"), ct);

        return Ok(new { revoked });
    }

    // ─────────────────────────── SIGN-IN HISTORY ────────────────────────────

    /// <summary>My settings: mere apne sign-in (kamyab + nakam), aakhri 30 din, max 20.</summary>
    [HttpGet("login-activity")]
    public async Task<ActionResult<IReadOnlyList<MeSignInDto>>> LoginActivity(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId) return Unauthorized();
        var since = DateTime.UtcNow.AddDays(-30);

        var items = await db.LoginAudits.AsNoTracking()
            .Where(a => a.UserId == userId && a.OccurredAt >= since && a.Method != LoginMethod.Refresh)
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Take(20)
            .Select(a => new MeSignInDto(a.Id, a.OccurredAt, a.Method, a.Succeeded, a.FailureReason, a.IpAddress, a.UserAgent))
            .ToListAsync(ct);

        return Ok(items);
    }
}
