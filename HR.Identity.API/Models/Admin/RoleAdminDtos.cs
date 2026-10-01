using System.ComponentModel.DataAnnotations;

namespace HR.Identity.API.Models.Admin;

public sealed record PermissionDto(int Id, string Code, string Module, string? Description, short SortOrder);

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<int> PermissionIds);

public sealed class SaveRoleRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public List<int> PermissionIds { get; set; } = [];
}

public sealed class DuplicateRoleRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
