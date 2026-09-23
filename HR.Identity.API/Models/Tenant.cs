using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models;

public class Tenant : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? LegalName { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? Phone { get; set; }
    public Guid? EntraTenantId { get; set; }
    public bool SsoEnabled { get; set; }
    public string Plan { get; set; } = "Free";
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public TenantSettings? Settings { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
