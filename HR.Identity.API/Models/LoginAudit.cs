using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models;

public class LoginAudit
{
    public long Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string EmailAttempted { get; set; } = default!;
    public LoginMethod Method { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
