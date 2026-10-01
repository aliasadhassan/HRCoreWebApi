using HR.Identity.API.Data;
using HR.Identity.API.Models;
using HR.Shared.Library.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Services;

public sealed record UserAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>
/// Access token hamesha DB ki TAAZA roles/permissions se banta hai —
/// login pe bhi aur har refresh pe bhi. Role badla to agle refresh (max ExpireMinutes) pe asar.
/// </summary>
public sealed class AccessTokenFactory(AppDbContext db, JwtTokenHelper jwt)
{
    public async Task<UserAccess> GetAccessAsync(Guid userId, CancellationToken ct = default)
    {
        var roles = await db.UserRoles
            .Where(ur => ur.UserId == userId && !ur.Role.IsDeleted)
            .Select(ur => ur.Role.Name)
            .OrderBy(n => n)
            .ToListAsync(ct);

        var permissions = await db.UserRoles
            .Where(ur => ur.UserId == userId && !ur.Role.IsDeleted)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return new UserAccess(roles, permissions);
    }

    public async Task<string> CreateAsync(User user, CancellationToken ct = default)
    {
        var access = await GetAccessAsync(user.Id, ct);
        return jwt.GenerateToken(new TokenSubject(
            user.Id, user.TenantId, user.Email, user.DisplayName, access.Roles, access.Permissions));
    }
}
