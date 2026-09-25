namespace HR.Employee.API.Application.Common.Interfaces;

/// <summary>JWT claims se logged-in user aur uska tenant.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }

    /// <summary>Tenant na mile to 403 — koi bhi write bina tenant ke nahi hona chahiye.</summary>
    Guid RequireTenantId();
}
