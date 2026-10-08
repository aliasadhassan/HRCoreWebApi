namespace HR.Payroll.API.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }

    /// <summary>JWT ke "perm" claims (HR.Shared.Library.Authorization.Permissions).</summary>
    /// <summary>Insaan ka naam (JWT "name") — audit log ke liye.</summary>
    string? Name => null;

    /// <summary>"POST api/payroll/runs/{id}/approve" — audit log mein kis kaam se badlaav aaya.</summary>
    string? Operation => null;

    bool HasPermission(string permission);

    Guid RequireTenantId();
}
