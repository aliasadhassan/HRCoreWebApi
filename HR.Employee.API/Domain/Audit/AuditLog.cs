namespace HR.Employee.API.Domain.Audit;

using HR.Employee.API.Domain.Common;
using HR.Shared.Library.Persistence;

/// <summary>
/// Audit page ka ek row: kisne, kab, kis record mein kya badla. Sirf likha jata hai — app role ke paas
/// is table pe UPDATE / DELETE grant hi nahi. AppDbContext har SaveChanges mein khud banata hai.
/// </summary>
public sealed class AuditLog : TenantChildEntity, IAuditRecord
{
    public DateTime At { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? EntityLabel { get; private set; }
    public Guid? SubjectEmployeeId { get; private set; }
    /// <summary>"POST api/leave/requests/{id}/approve" — kis kaam se ye badlaav aaya.</summary>
    public string? Operation { get; private set; }
    /// <summary>Ek request ke saare badlaav ek hi id — Activity mein ek line.</summary>
    public Guid CorrelationId { get; private set; }
    public string Changes { get; private set; } = "[]";

    private AuditLog() { }

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
