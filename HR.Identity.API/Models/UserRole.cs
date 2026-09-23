namespace HR.Identity.API.Models;

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssignedBy { get; set; }

    public User User { get; set; } = default!;
    public Role Role { get; set; } = default!;
}
