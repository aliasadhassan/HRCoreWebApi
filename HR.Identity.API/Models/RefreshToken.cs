namespace HR.Identity.API.Models;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] TokenHash { get; set; } = default!;      // SHA-256, raw token kabhi save nahi
    public Guid FamilyId { get; set; }                     // rotation chain
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public User User { get; set; } = default!;

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
