namespace HR.Identity.API.Models;

public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;           // "employees.view"
    public string Module { get; set; } = default!;         // "Employees"
    public string? Description { get; set; }
    public short SortOrder { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
