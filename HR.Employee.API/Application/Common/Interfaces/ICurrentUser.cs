namespace HR.Employee.API.Application.Common.Interfaces;

/// <summary>JWT claims se logged-in user aur uska tenant.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }

    /// <summary>Insaan ka naam (JWT "name") — audit log ke liye.</summary>
    string? Name => null;

    /// <summary>"POST api/leave/{id}/approve" — audit log mein kis kaam se badlaav aaya.</summary>
    string? Operation => null;

    /// <summary>JWT ke "perm" claims (HR.Shared.Library.Authorization.Permissions).</summary>
    bool HasPermission(string permission);

    /// <summary>Tenant na mile to 403 — koi bhi write bina tenant ke nahi hona chahiye.</summary>
    Guid RequireTenantId();
}
