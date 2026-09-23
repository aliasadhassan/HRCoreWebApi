using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models;

public class UserToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TokenPurpose Purpose { get; set; }
    public byte[] TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = default!;
}
