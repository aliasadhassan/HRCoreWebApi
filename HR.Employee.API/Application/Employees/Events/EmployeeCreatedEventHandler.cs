using HR.Employee.API.Domain.Events;
using HR.Employee.API.Domain.Interfaces;
using MediatR;

namespace HR.Employee.API.Application.Employees.Events
{
    public sealed class EmployeeCreatedEventHandler : INotificationHandler<EmployeeCreatedEvent>
    {
        private readonly IMessagePublisher _publisher;

        public EmployeeCreatedEventHandler(IMessagePublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task Handle(EmployeeCreatedEvent notification, CancellationToken cancellationToken)
        {
            await _publisher.PublishAsync(notification, "hr-employee-added-queue", cancellationToken);
        }
    }
}
