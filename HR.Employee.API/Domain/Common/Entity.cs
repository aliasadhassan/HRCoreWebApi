namespace HR.Employee.API.Domain.Common;

using MediatR;

/// <summary>Domain event marker. MediatR notification ke zariye SaveChanges se pehle dispatch hota hai.</summary>
public interface IDomainEvent : INotification
{
}

/// <summary>Har entity ka base: Id + domain events.</summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; protected set; }   // EF sequential Guid generate karta hai (Add ke waqt)

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Aggregate ka child row (punch, policy rule) jo doosri tenant table ko bhi point karta hai.
/// Apna TenantId zaroori hai taake composite FK aur RLS isay bhi check kar saken. AppDbContext Add pe khud set karta hai.
/// </summary>
public abstract class TenantChildEntity : Entity
{
    public Guid TenantId { get; internal set; }
}

/// <summary>Tenant-owned, audited, soft-deletable entity. Audit fields AppDbContext khud set karta hai.</summary>
public abstract class AuditableEntity : Entity
{
    public Guid TenantId { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public Guid? CreatedBy { get; internal set; }
    public DateTime? UpdatedAt { get; internal set; }
    public Guid? UpdatedBy { get; internal set; }
    public bool IsDeleted { get; internal set; }
    /// <summary>Postgres ka xmin system column — har update pe khud badalta hai (bytea rowversion Postgres mein generate nahi hota).</summary>
    public uint RowVersion { get; private set; }
}
