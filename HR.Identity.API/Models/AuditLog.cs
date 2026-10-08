using HR.Shared.Library.Persistence;

namespace HR.Identity.API.Models;

/// <summary>
/// Audit page (access ka hissa): users, roles, permissions, company settings mein kisne kya badla.
/// Sirf likha jata hai — hr_identity_app ke paas UPDATE / DELETE grant nahi. AppDbContext.SaveChangesAsync khud banata hai.
/// </summary>
public class AuditLog : IAuditRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime At { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string? EntityLabel { get; set; }
    public Guid? SubjectEmployeeId { get; set; }
    public string? Operation { get; set; }
    public Guid CorrelationId { get; set; }
    public string Changes { get; set; } = "[]";

    public static AuditLog From(Guid tenantId, AuditCapture c, DateTime at, Guid? userId, string? userName, string? operation, Guid correlationId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            At = at,
            UserId = userId,
            UserName = Cut(userName, 150),
            Action = c.Action,
            EntityType = Cut(c.EntityType, 80)!,
            EntityId = c.EntityId,
            EntityLabel = Cut(c.Label, 200),
            SubjectEmployeeId = c.SubjectEmployeeId,
            Operation = Cut(operation, 200),
            CorrelationId = correlationId,
            Changes = AuditTrail.Serialize(c.Changes)
        };

    private static string? Cut(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
