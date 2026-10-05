namespace HR.Payroll.API.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }

    /// <summary>JWT ke "perm" claims (HR.Shared.Library.Authorization.Permissions).</summary>
    bool HasPermission(string permission);

    Guid RequireTenantId();
}
