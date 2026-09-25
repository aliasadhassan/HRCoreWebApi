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

/// <summary>Tenant-owned, audited, soft-deletable entity. Audit fields AppDbContext khud set karta hai.</summary>
public abstract class AuditableEntity : Entity
{
    public Guid TenantId { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public Guid? CreatedBy { get; internal set; }
    public DateTime? UpdatedAt { get; internal set; }
    public Guid? UpdatedBy { get; internal set; }
    public bool IsDeleted { get; internal set; }
    public byte[] RowVersion { get; private set; } = default!;
}
