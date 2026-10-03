using System.Net;
using HR.Identity.API.Configuration;
using HR.Identity.API.Data;
using HR.Identity.API.Helpers;
using HR.Identity.API.Models;
using HR.Identity.API.Models.Admin;
using HR.Identity.API.Models.Common;
using HR.Identity.API.Services;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HR.Identity.API.Controllers;

/// <summary>
/// Company ke login users. Har query current tenant tak mehdood.
/// Hifazat: khud ko band nahi kar sakte, apna admin role nahi hata sakte, aakhri admin kabhi nahi hatega.
/// </summary>
[ApiController]
[Route("api/users")]
[HasPermission(Permissions.UsersManage)]
public sealed class UsersController(
    AppDbContext db,
    RefreshTokenService refreshTokens,
    IEmailService email,
    IPublishEndpoint publish,
    IOptions<AuthSettings> authSettings,
    ILogger<UsersController> logger) : ControllerBase
{
    private const string AdminRoleName = "TENANT ADMIN";
    private const int InviteExpiryHours = 72;
    private const int ResetExpiryHours = 1;

    private Guid TenantId => User.GetTenantId() ?? throw new UnauthorizedAccessException("Tenant missing in token.");
    private Guid MeId => User.GetUserId() ?? throw new UnauthorizedAccessException("User missing in token.");

    // ───────────────────────────── Queries ─────────────────────────────
    [HttpGet]
    public async Task<ActionResult<PagedDto<UserListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] UserStatus? status,
        [FromQuery] Guid? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var now = DateTime.UtcNow;

        var query = TenantUsers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.DisplayName.Contains(term) || u.Email.Contains(term));
        }

        if (roleId is { } rid)
            query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == rid));

        query = status switch
        {
            UserStatus.Disabled => query.Where(u => !u.IsActive),
            UserStatus.Locked => query.Where(u => u.IsActive && u.LockoutEnd != null && u.LockoutEnd > now),
            UserStatus.Invited => query.Where(IsInvited(now)),
            UserStatus.Active => query.Where(u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= now)
                                                 && (u.PasswordHash != null || u.ExternalLogins.Any() || u.EmailConfirmed)),
            _ => query
        };

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.AvatarUrl,
                u.IsActive,
                u.LockoutEnd,
                u.LastLoginAt,
                u.CreatedAt,
                u.EmailConfirmed,
                HasPassword = u.PasswordHash != null,
                IsSso = u.ExternalLogins.Any(),
                Roles = u.UserRoles
                    .Where(ur => !ur.Role.IsDeleted)
                    .Select(ur => new RoleRefDto(ur.Role.Id, ur.Role.Name, ur.Role.IsSystem))
                    .ToList()
            })
            .ToListAsync(ct);

        var me = MeId;
        var items = rows.Select(r => new UserListItemDto(
            r.Id, r.Email, r.DisplayName, r.AvatarUrl,
            r.Roles.OrderBy(x => x.Name).ToList(),
            StatusOf(r.IsActive, r.LockoutEnd, r.HasPassword, r.IsSso, r.EmailConfirmed, now),
            r.IsSso, r.Id == me, r.LastLoginAt, r.LockoutEnd, r.CreatedAt)).ToList();

        return Ok(new PagedDto<UserListItemDto>(items, total, page, pageSize));
    }

    /// <summary>Filter tabs ke numbers.</summary>
    [HttpGet("counts")]
    public async Task<ActionResult<UserCountsDto>> Counts(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = TenantUsers();
        return Ok(new UserCountsDto(
            await q.CountAsync(ct),
            await q.CountAsync(u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= now)
                                    && (u.PasswordHash != null || u.ExternalLogins.Any() || u.EmailConfirmed), ct),
            await q.CountAsync(IsInvited(now), ct),
            await q.CountAsync(u => u.IsActive && u.LockoutEnd != null && u.LockoutEnd > now, ct),
            await q.CountAsync(u => !u.IsActive, ct)));
    }

    /// <summary>Role picker: system roles + is company ke apne roles.</summary>
    [HttpGet("assignable-roles")]
    public async Task<ActionResult<IReadOnlyList<AssignableRoleDto>>> AssignableRoles(CancellationToken ct)
    {
        var tenantId = TenantId;
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.TenantId == null || r.TenantId == tenantId)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new AssignableRoleDto(
                r.Id, r.Name, r.Description, r.IsSystem,
                r.UserRoles.Count(ur => ur.User.TenantId == tenantId && !ur.User.IsDeleted)))
            .ToListAsync(ct);
        return Ok(roles);
    }

    // ───────────────────────────── Invite ──────────────────────────────
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        var tenantId = TenantId;
        var emailAddress = request.Email.Trim();
        var normalized = emailAddress.ToUpperInvariant();

        if (await db.Users.AnyAsync(u => u.TenantId == tenantId && u.NormalizedEmail == normalized, ct))
            return Problem(statusCode: 409, detail: "A user with this email already exists.");

        var roleIds = await ValidRoleIdsAsync(request.RoleIds, ct);
        if (roleIds is null)
            return Problem(statusCode: 400, detail: "One or more roles are not available.");

        var me = MeId;
        var user = new User
        {
            TenantId = tenantId,
            Email = emailAddress,
            NormalizedEmail = normalized,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = null,          // invite accept pe khud set karega
            EmailConfirmed = false,
            CreatedBy = me
        };
        foreach (var rid in roleIds)
            user.UserRoles.Add(new UserRole { RoleId = rid, AssignedBy = me });

        db.Users.Add(user);
        var token = AddToken(user, TokenPurpose.Invite, InviteExpiryHours);
        await db.SaveChangesAsync(ct);

        await publish.Publish(new UserCreatedEvent(user.Id, user.TenantId, user.DisplayName, user.Email), ct);
        await SendInviteAsync(user, token);

        return Created($"api/users/{user.Id}", new { id = user.Id });
    }

    [HttpPost("{id:guid}/resend-invite")]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user is null) return NotFound();
        if (user.PasswordHash is not null || user.EmailConfirmed)
            return Problem(statusCode: 409, detail: "This user has already accepted the invitation.");

        await ExpireOpenTokensAsync(user.Id, TokenPurpose.Invite, ct);
        var token = AddToken(user, TokenPurpose.Invite, InviteExpiryHours);
        await db.SaveChangesAsync(ct);
        await SendInviteAsync(user, token);
        return NoContent();
    }

    // ───────────────────────────── Update ──────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();

        var roleIds = await ValidRoleIdsAsync(request.RoleIds, ct);
        if (roleIds is null)
            return Problem(statusCode: 400, detail: "One or more roles are not available.");

        var adminRoleId = await AdminRoleIdAsync(ct);
        var wasAdmin = user.UserRoles.Any(ur => ur.RoleId == adminRoleId);
        var willBeAdmin = roleIds.Contains(adminRoleId);

        if (wasAdmin && !willBeAdmin)
        {
            if (user.Id == MeId)
                return Problem(statusCode: 409, detail: "You can't remove your own administrator role.");
            if (await ActiveAdminCountAsync(adminRoleId, ct) <= 1)
                return Problem(statusCode: 409, detail: "The company must keep at least one active administrator.");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.UpdatedBy = MeId;

        foreach (var removed in user.UserRoles.Where(ur => !roleIds.Contains(ur.RoleId)).ToList())
            user.UserRoles.Remove(removed);
        foreach (var added in roleIds.Where(rid => user.UserRoles.All(ur => ur.RoleId != rid)))
            user.UserRoles.Add(new UserRole { RoleId = added, AssignedBy = MeId });

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ───────────────────────────── Status ──────────────────────────────
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user is null) return NotFound();
        if (user.Id == MeId)
            return Problem(statusCode: 409, detail: "You can't deactivate your own account.");

        var adminRoleId = await AdminRoleIdAsync(ct);
        var isAdmin = await db.UserRoles.AnyAsync(ur => ur.UserId == id && ur.RoleId == adminRoleId, ct);
        if (isAdmin && user.IsActive && await ActiveAdminCountAsync(adminRoleId, ct) <= 1)
            return Problem(statusCode: 409, detail: "The company must keep at least one active administrator.");

        user.IsActive = false;
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedBy = MeId;
        await db.SaveChangesAsync(ct);
        await refreshTokens.RevokeAllForUserAsync(user.Id, "Deactivated");
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user is null) return NotFound();
        user.IsActive = true;
        user.UpdatedBy = MeId;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user is null) return NotFound();
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.UpdatedBy = MeId;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/send-password-reset")]
    public async Task<IActionResult> SendPasswordReset(Guid id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user is null) return NotFound();
        if (user.PasswordHash is null)
            return Problem(statusCode: 409, detail: "This user signs in with Microsoft or hasn't accepted the invitation yet.");

        await ExpireOpenTokensAsync(user.Id, TokenPurpose.PasswordReset, ct);
        var token = AddToken(user, TokenPurpose.PasswordReset, ResetExpiryHours);
        await db.SaveChangesAsync(ct);

        var link = Link(user, token, invite: false);
        await SendAsync(user.Email, "Reset your HR Cloud password", EmailBody(
            $"Hello {WebUtility.HtmlEncode(user.DisplayName)},",
            "Your administrator asked us to send you a link to set a new password.",
            link, "Set a new password", $"This link expires in {ResetExpiryHours} hour."));
        return NoContent();
    }

    /// <summary>Har device se logout — phone chori, ya shak ho ke account kisi aur ke paas hai.</summary>
    [HttpPost("{id:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        if (user is null) return NotFound();
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedBy = MeId;
        await db.SaveChangesAsync(ct);
        await refreshTokens.RevokeAllForUserAsync(user.Id, "RevokedByAdmin");
        return NoContent();
    }

    // ───────────────────────────── Helpers ─────────────────────────────
    private IQueryable<User> TenantUsers()
    {
        var tenantId = TenantId;
        return db.Users.AsNoTracking().Where(u => u.TenantId == tenantId);
    }

    private Task<User?> FindAsync(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        return db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct);
    }

    /// <summary>Invite bheja, abhi password set nahi kiya aur SSO se bhi nahi aaya.</summary>
    private static System.Linq.Expressions.Expression<Func<User, bool>> IsInvited(DateTime now)
        => u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= now) && u.PasswordHash == null && !u.EmailConfirmed && !u.ExternalLogins.Any();

    private static UserStatus StatusOf(bool isActive, DateTime? lockoutEnd, bool hasPassword, bool isSso, bool emailConfirmed, DateTime now)
    {
        if (!isActive) return UserStatus.Disabled;
        if (lockoutEnd > now) return UserStatus.Locked;
        if (!hasPassword && !isSso && !emailConfirmed) return UserStatus.Invited;
        return UserStatus.Active;
    }

    /// <summary>Sirf wo roles jo is company ko assign ho sakte hain. Koi bhi ghalat id → null.</summary>
    private async Task<List<Guid>?> ValidRoleIdsAsync(IEnumerable<Guid> requested, CancellationToken ct)
    {
        var ids = requested.Distinct().ToList();
        if (ids.Count == 0) return null;
        var tenantId = TenantId;
        var valid = await db.Roles
            .Where(r => ids.Contains(r.Id) && (r.TenantId == null || r.TenantId == tenantId))
            .Select(r => r.Id)
            .ToListAsync(ct);
        return valid.Count == ids.Count ? valid : null;
    }

    private Task<Guid> AdminRoleIdAsync(CancellationToken ct)
        => db.Roles.Where(r => r.TenantId == null && r.NormalizedName == AdminRoleName)
                   .Select(r => r.Id).FirstAsync(ct);

    private Task<int> ActiveAdminCountAsync(Guid adminRoleId, CancellationToken ct)
    {
        var tenantId = TenantId;
        return db.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId
                                             && ur.User.TenantId == tenantId
                                             && ur.User.IsActive
                                             && !ur.User.IsDeleted, ct);
    }

    private string AddToken(User user, TokenPurpose purpose, int hours)
    {
        var raw = TokenHasher.NewToken();
        db.UserTokens.Add(new UserToken
        {
            User = user,
            Purpose = purpose,
            TokenHash = TokenHasher.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddHours(hours)
        });
        return raw;
    }

    private Task ExpireOpenTokensAsync(Guid userId, TokenPurpose purpose, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
    }

    private string Link(User user, string token, bool invite)
        => $"{authSettings.Value.ResetPasswordUrl}?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(user.Email)}"
           + (invite ? "&invite=1" : string.Empty);

    private Task SendInviteAsync(User user, string token)
        => SendAsync(user.Email, "You're invited to HR Cloud", EmailBody(
            $"Hello {WebUtility.HtmlEncode(user.DisplayName)},",
            "You have been invited to join your company's HR Cloud workspace. Set your password to get started.",
            Link(user, token, invite: true), "Accept invitation",
            $"This invitation expires in {InviteExpiryHours} hours."));

    /// <summary>Email fail ho to user phir bhi bana rahe — admin "Resend invite" se dobara bhej sakta hai.</summary>
    private async Task SendAsync(string to, string subject, string html)
    {
        try
        {
            await email.SendEmailAsync(to, subject, html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send '{Subject}' to {Email}", subject, to);
        }
    }

    private static string EmailBody(string greeting, string text, string link, string button, string note) => $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;color:#232823">
          <h2 style="font-family:Georgia,serif;color:#13211a;margin:0 0 16px">HR Cloud</h2>
          <p>{greeting}</p>
          <p>{text}</p>
          <p style="margin:28px 0">
            <a href="{link}" style="background:#13211a;color:#eceee6;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600">{button}</a>
          </p>
          <p style="color:#636a62;font-size:13px">{note} If you weren't expecting this email, you can ignore it.</p>
        </div>
        """;
}
