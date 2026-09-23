namespace HR.Identity.API.Models;

public class RolePermission
{
    public Guid RoleId { get; set; }
    public int PermissionId { get; set; }

    public Role Role { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}
