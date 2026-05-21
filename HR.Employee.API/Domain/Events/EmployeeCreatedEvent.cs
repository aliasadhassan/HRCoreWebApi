using MediatR;

namespace HR.Employee.API.Domain.Events;

public sealed record EmployeeCreatedEvent(
    Guid EmployeeId,
    string Email,
    string Department,
    decimal Salary) : INotification;
