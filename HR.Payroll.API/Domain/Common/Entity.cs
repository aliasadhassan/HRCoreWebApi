namespace HR.Payroll.API.Domain.Common;

using MediatR;

public interface IDomainEvent : INotification
{
}

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Aggregate ka child row (payslip line, salary component) jo doosri tenant table (PayComponents) ko bhi point karta hai.
/// Apna TenantId zaroori hai taake composite FK aur RLS isay bhi check kar saken. AppDbContext Add pe khud set karta hai.
/// </summary>
public abstract class TenantChildEntity : Entity
{
    public Guid TenantId { get; internal set; }
}

/// <summary>Audit + soft delete + concurrency. AppDbContext khud set karta hai.</summary>
public abstract class AuditableBase : Entity
{
    public DateTime CreatedAt { get; internal set; }
    public Guid? CreatedBy { get; internal set; }
    public DateTime? UpdatedAt { get; internal set; }
    public Guid? UpdatedBy { get; internal set; }
    public bool IsDeleted { get; internal set; }
    /// <summary>Postgres ka xmin system column — har update pe khud badalta hai (bytea rowversion Postgres mein generate nahi hota).</summary>
    public uint RowVersion { get; private set; }
}

/// <summary>Sirf ek tenant ka data.</summary>
public abstract class AuditableEntity : AuditableBase
{
    public Guid TenantId { get; internal set; }
}

/// <summary>
/// TenantId NULL = platform ka data (tax regimes, contribution rules) jo sab tenants dekhte hain.
/// Tenant apna custom version bhi bana sakta hai.
/// </summary>
public abstract class SharedAuditableEntity : AuditableBase
{
    public Guid? TenantId { get; internal set; }
    public bool IsPlatformDefined => TenantId is null;
}
