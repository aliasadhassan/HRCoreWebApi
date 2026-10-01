using System.Security.Claims;
using HR.Shared.Library.Helpers;

namespace HR.Shared.Library.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid? GetTenantId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(JwtTokenHelper.TenantClaim), out var id) ? id : null;

    public static bool HasPermission(this ClaimsPrincipal user, string permission)
        => user.HasClaim(JwtTokenHelper.PermissionClaim, permission);
}
