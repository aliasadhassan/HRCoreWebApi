using System.ComponentModel.DataAnnotations;

namespace HR.Identity.API.Models.Admin;

public enum UserStatus { Active, Invited, Locked, Disabled }

public sealed record RoleRefDto(Guid Id, string Name, bool IsSystem);

public sealed record UserListItemDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    IReadOnlyList<RoleRefDto> Roles,
    UserStatus Status,
    bool IsSso,
    bool IsYou,
    DateTime? LastLoginAt,
    DateTime? LockoutEnd,
    DateTime CreatedAt);

public sealed record PagedDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record UserCountsDto(int All, int Active, int Invited, int Locked, int Disabled);

public sealed record AssignableRoleDto(Guid Id, string Name, string? Description, bool IsSystem, int UserCount);

public sealed class InviteUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "Choose at least one role.")]
    public List<Guid> RoleIds { get; set; } = [];
}

public sealed class UpdateUserRequest
{
    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "Choose at least one role.")]
    public List<Guid> RoleIds { get; set; } = [];
}
