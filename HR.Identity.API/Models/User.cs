using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models;

public class User : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = default!;
    public string NormalizedEmail { get; set; } = default!;
    public string? PasswordHash { get; set; }             // null = SSO-only user
    public string DisplayName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public Guid? EmployeeId { get; set; }                 // logical link to Employee_Db
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public byte AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public Tenant Tenant { get; set; } = default!;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserToken> Tokens { get; set; } = new List<UserToken>();
}
