namespace HR.Employee.API.Application.Employees.EventHandlers;

using HR.Employee.API.Domain.Employees;
using HR.Shared.Library.Events;
using MassTransit;
using MediatR;

/// <summary>
/// Domain event → integration event.
/// IPublishEndpoint (Bus Outbox) message ko OutboxMessage table mein likhta hai, ISI SaveChanges ke saath.
/// DB fail = message bhi nahi jayega. Dual-write khatam.
/// </summary>
public sealed class EmployeeCreatedHandler(IPublishEndpoint publishEndpoint) : INotificationHandler<EmployeeCreatedDomainEvent>
{
    public Task Handle(EmployeeCreatedDomainEvent notification, CancellationToken ct)
    {
        var e = notification.Employee;
        return publishEndpoint.Publish(new EmployeeCreatedIntegrationEvent(
            e.Id, e.TenantId, e.EmployeeCode, e.FullName, e.WorkEmail,
            e.DepartmentId, e.LocationId, e.EmploymentType.ToString(), e.JoiningDate), ct);
    }
}

public sealed class EmployeeExitedHandler(IPublishEndpoint publishEndpoint) : INotificationHandler<EmployeeExitedDomainEvent>
{
    public Task Handle(EmployeeExitedDomainEvent notification, CancellationToken ct)
    {
        var e = notification.Employee;
        return publishEndpoint.Publish(new EmployeeExitedIntegrationEvent(
            e.Id, e.TenantId, e.UserId, e.ExitDate!.Value), ct);
    }
}
