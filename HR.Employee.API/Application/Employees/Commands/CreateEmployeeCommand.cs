using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;
using HR.Employee.API.Domain.Interfaces;
using MediatR;

namespace HR.Employee.API.Application.Employees.Commands;

// .NET 8 Record - Requests ke liye data transfer object (DTO) ka kaam karega
public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    string Department,
    decimal Salary) : IRequest<Guid>;

// MediatR Command Handler
public sealed class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Guid>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork; // Naya addition

    // Dependency Injection
    public CreateEmployeeCommandHandler(IEmployeeRepository employeeRepository, IUnitOfWork unitOfWork)
    {
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        // Domain ki rich factory method call kar ke object initialize karna
        var employee = EmployeeEntity.Create(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Department,
            request.Salary
        );

        // Domain repository ke zariye save call karna
        await _employeeRepository.AddAsync(employee, cancellationToken);

        // 3. Commit changes to Database via Unit of Work
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Id return karna taake frontend ko create hui entity ka pata chal sakay
        return employee.Id;
    }
}
