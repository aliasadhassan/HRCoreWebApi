using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;
using HR.Employee.API.Domain.Interfaces;
using MediatR;

namespace HR.Employee.API.Application.Employees.Queries;

// Query Record - Sirf ID input le ga
public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<EmployeeResponse?>;

// Optimized Response DTO (Data Transfer Object) - Taake exact field bahar jayein
public sealed record EmployeeResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Department,
    decimal Salary,
    bool IsActive);

// MediatR Query Handler
public sealed class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeResponse?>
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeByIdQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeResponse?> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        // Repository se data fetch karna
        var employee = await _employeeRepository.GetByIdAsync(request.Id, cancellationToken);

        if (employee == null) return null;

        // Domain model ko safe Response DTO mein map karna
        return new EmployeeResponse(
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.Department,
            employee.Salary,
            employee.IsActive
        );
    }
}
