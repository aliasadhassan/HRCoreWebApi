namespace HR.Identity.API.Models;

public class UserExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = default!;       // "Microsoft"
    public string ProviderKey { get; set; } = default!;    // Entra 'oid'
    public string? ProviderTenantId { get; set; }          // Entra 'tid'
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    public User User { get; set; } = default!;
}
