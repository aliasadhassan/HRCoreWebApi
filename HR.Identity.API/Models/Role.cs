using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models;

public class Role : AuditableEntity
{
    public Guid? TenantId { get; set; }                    // null = system role
    public string Name { get; set; } = default!;
    public string NormalizedName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
