namespace HR.Payroll.API.Infrastructure.Identity;

using System.Security.Claims;
using HR.Payroll.API.Application.Common.Interfaces;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string TenantClaim = "tenant_id";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Parse(Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Principal?.FindFirst("sub")?.Value);

    public Guid? TenantId => Parse(Principal?.FindFirst(TenantClaim)?.Value);

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public Guid RequireTenantId()
        => TenantId ?? throw new UnauthorizedAccessException("Tenant information is missing from the access token.");

    private static Guid? Parse(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
