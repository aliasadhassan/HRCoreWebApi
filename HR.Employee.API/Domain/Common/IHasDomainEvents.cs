using MediatR;

namespace HR.Employee.API.Domain.Common;
public interface IHasDomainEvents
{
    IReadOnlyCollection<INotification> DomainEvents { get; }
    void AddDomainEvent(INotification domainEvent);
    void ClearDomainEvents();
}
